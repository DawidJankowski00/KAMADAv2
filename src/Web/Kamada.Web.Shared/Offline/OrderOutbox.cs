using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;
using Kamada.Web.Shared.Connectivity;

namespace Kamada.Web.Shared.Offline;

/// <summary>
/// Orders taken on the phone without a (good) connection, kept on the device and sent when the connection is back:
/// in the order they were taken, each in steps (client → address → order) with its request ids.
/// </summary>
/// <remarks>
/// A transport failure (no network, timeout, 5xx, an expired session, a concurrency conflict) stops the run and the entry
/// waits for the next one; a refusal (validation, not found, conflict such as not enough stock) marks the entry
/// <see cref="OutboxStatus.NeedsAttention"/> and the run continues with the next entry. Runs start when the app starts,
/// after an order is queued (unless offline), when the connection becomes good and on demand.
/// </remarks>
public sealed class OrderOutbox : IDisposable
{
    private readonly ILocalStore _store;
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private readonly ConnectionMonitor _monitor;
    private readonly OfflineLookup _lookup;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private List<OutboxEntry> _entries = [];
    private bool _started;

    public OrderOutbox(ILocalStore store, ApiClient api, AuthService auth, ConnectionMonitor monitor, OfflineLookup lookup, TimeProvider clock)
    {
        _store = store;
        _api = api;
        _auth = auth;
        _monitor = monitor;
        _lookup = lookup;
        _clock = clock;
    }

    /// <summary>Raised when entries were added, sent, changed or removed.</summary>
    public event Action? Changed;

    /// <summary>Raised for every queued order the server accepted.</summary>
    public event Action<OrderDto>? OrderSent;

    /// <summary>Oldest first (the sending order).</summary>
    public IReadOnlyList<OutboxEntry> Entries => _entries;

    public int NeedsAttentionCount => _entries.Count(e => e.Status == OutboxStatus.NeedsAttention);

    /// <summary>The entry being sent right now; it cannot be edited or discarded meanwhile.</summary>
    public Guid? SendingId { get; private set; }

    public bool IsSending => SendingId is not null;

    /// <summary>Loads the signed-in worker's entries, follows session and connection changes and sends what is waiting.</summary>
    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            _auth.SessionChanged += OnSessionChanged;
            _monitor.QualityChanged += OnQualityChanged;
        }

        await LoadAsync();
        await SendAsync();
    }

    public async Task EnqueueAsync(OutboxEntry entry)
    {
        await SaveAsync(entry);
        if (_monitor.Quality != ConnectionQuality.Offline)
        {
            _ = SendAsync();
        }
    }

    public OutboxEntry? Find(Guid id) => _entries.SingleOrDefault(e => e.Id == id);

    /// <summary>Replaces an entry edited by the worker; it is sent again with the next run.</summary>
    public Task UpdateAsync(OutboxEntry entry) =>
        EnqueueAsync(entry with { Status = OutboxStatus.Pending, Problem = null, ProblemCode = null });

    /// <summary>Removes an entry for good (the worker gave up on the order).</summary>
    public async Task<bool> DiscardAsync(Guid id)
    {
        if (id == SendingId)
        {
            return false;
        }

        await _store.DeleteAsync(LocalStoreName.Outbox, Key(id));
        _entries.RemoveAll(e => e.Id == id);
        Changed?.Invoke();
        return true;
    }

    /// <summary>Sends the waiting entries; returns how many orders the server accepted. A run already going on is not doubled.</summary>
    public async Task<int> SendAsync(CancellationToken cancellationToken = default)
    {
        if (!await _sendLock.WaitAsync(0, cancellationToken))
        {
            return 0;
        }

        var sent = 0;
        try
        {
            foreach (var entry in _entries.Where(e => e.Status == OutboxStatus.Pending).ToList())
            {
                SendingId = entry.Id;
                Changed?.Invoke();
                var outcome = await SendEntryAsync(entry, cancellationToken);
                if (outcome == SendOutcome.Sent)
                {
                    sent++;
                }
                else if (outcome == SendOutcome.TryLater)
                {
                    break;
                }
            }
        }
        finally
        {
            SendingId = null;
            _sendLock.Release();
            Changed?.Invoke();
        }

        if (sent > 0)
        {
            // Stock went down and new clients exist now: keep the device's copy current.
            await _lookup.RefreshAsync(cancellationToken);
        }

        return sent;
    }

    public void Dispose()
    {
        if (_started)
        {
            _auth.SessionChanged -= OnSessionChanged;
            _monitor.QualityChanged -= OnQualityChanged;
        }

        _sendLock.Dispose();
    }

    /// <summary>Whether a failed call may succeed later without any change to the entry.</summary>
    public static bool IsTransient(ApiProblem problem) =>
        problem.Status is 0 or 401 or 408 or 429 or >= 500 || problem.Code == "Concurrency.Conflict";

    private static string Key(Guid id) => id.ToString("N");

    private async Task LoadAsync()
    {
        // The lookup applies the device's owner rule first (another worker's entries are wiped).
        await _lookup.LoadAsync();
        _entries = await _auth.GetCurrentUserAsync() is null
            ? []
            : [.. (await _store.GetAllAsync<OutboxEntry>(LocalStoreName.Outbox)).OrderBy(e => e.CreatedAt)];
        Changed?.Invoke();
    }

    private async Task<SendOutcome> SendEntryAsync(OutboxEntry entry, CancellationToken cancellationToken)
    {
        entry = entry with { Attempts = entry.Attempts + 1, LastAttemptAt = _clock.GetUtcNow() };

        if (entry.NewClient is { } newClient && entry.ClientId is null)
        {
            var created = await _api.PostAsync<ClientDto>("api/clients", newClient, cancellationToken);
            if (!created.IsSuccess)
            {
                return await FailAsync(entry, created.Problem!);
            }

            var client = created.Value;
            entry = entry with
            {
                ClientId = client.Id,
                AddressId = client.Addresses.First(a => a.Type == AddressType.Shipping).Id,
                Client = new ClientLookupDto(
                    client.Id, client.Username, client.Email, client.Name, client.Surname, client.PhoneNumber,
                    [.. client.Addresses.Where(a => a.Type == AddressType.Shipping)]),
            };
            await SaveAsync(entry);
        }

        if (entry.NewAddress is { } newAddress && entry.AddressId is null)
        {
            var added = await _api.PostAsync<AddressDto>($"api/clients/{entry.ClientId}/addresses", newAddress, cancellationToken);
            if (!added.IsSuccess)
            {
                return await FailAsync(entry, added.Problem!);
            }

            entry = entry with
            {
                AddressId = added.Value.Id,
                Client = entry.Client with { ShippingAddresses = [.. entry.Client.ShippingAddresses, added.Value] },
            };
            await SaveAsync(entry);
        }

        var placed = await _api.PostAsync<OrderDto>("api/orders", entry.ToOrderRequest(), cancellationToken);
        if (!placed.IsSuccess)
        {
            return await FailAsync(entry, placed.Problem!);
        }

        await _store.DeleteAsync(LocalStoreName.Outbox, Key(entry.Id));
        _entries.RemoveAll(e => e.Id == entry.Id);
        OrderSent?.Invoke(placed.Value);
        return SendOutcome.Sent;
    }

    private async Task<SendOutcome> FailAsync(OutboxEntry entry, ApiProblem problem)
    {
        if (IsTransient(problem))
        {
            await SaveAsync(entry);
            return SendOutcome.TryLater;
        }

        await SaveAsync(entry with { Status = OutboxStatus.NeedsAttention, Problem = problem.Message, ProblemCode = problem.Code });
        return SendOutcome.Refused;
    }

    private async Task SaveAsync(OutboxEntry entry)
    {
        await _store.PutAsync(LocalStoreName.Outbox, Key(entry.Id), entry);
        var index = _entries.FindIndex(e => e.Id == entry.Id);
        if (index >= 0)
        {
            _entries[index] = entry;
        }
        else
        {
            _entries = [.. _entries.Append(entry).OrderBy(e => e.CreatedAt)];
        }

        Changed?.Invoke();
    }

    private async void OnSessionChanged()
    {
        try
        {
            await LoadAsync();
            await SendAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async void OnQualityChanged(ConnectionQuality quality)
    {
        if (quality != ConnectionQuality.Good)
        {
            return;
        }

        try
        {
            await SendAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private enum SendOutcome
    {
        Sent,
        Refused,
        TryLater,
    }
}
