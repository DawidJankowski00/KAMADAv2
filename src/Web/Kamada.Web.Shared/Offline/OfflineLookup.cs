using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;
using Kamada.Web.Shared.Connectivity;

namespace Kamada.Web.Shared.Offline;

/// <summary>
/// The device's copy of the active products and clients, so an order can be prepared without a connection.
/// Downloaded only for a worker working as a seller, refreshed after login, after switching to such a role and
/// when the connection becomes good again (cheaply: the server answers 304 while nothing changed).
/// </summary>
/// <remarks>
/// The copy holds clients' personal data, so it never outlives the session: signing out (<see cref="ForgetAsync"/>) wipes
/// everything on the device; an expired session wipes the copy but keeps queued orders, which the same worker sends after
/// signing in again; another worker signing in on the device starts from an empty store.
/// </remarks>
public sealed class OfflineLookup : IDisposable
{
    public const string ProductsUrl = "api/products/lookup";
    public const string ClientsUrl = "api/clients/lookup";

    /// <summary>
    /// Roles whose phone area takes orders without a connection. Admins take orders on the phone too, but only online:
    /// the less personal data on devices, the better.
    /// </summary>
    public static readonly IReadOnlyList<string> OfflineRoles = [Roles.Seller];

    private const string ProductsKey = "products";
    private const string ClientsKey = "clients";
    private const string OwnerKey = "owner";

    private readonly ILocalStore _store;
    private readonly ApiClient _api;
    private readonly AuthService _auth;
    private readonly RoleContext _roles;
    private readonly ConnectionMonitor _monitor;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private Guid? _loadedFor;
    private bool _started;

    public OfflineLookup(ILocalStore store, ApiClient api, AuthService auth, RoleContext roles, ConnectionMonitor monitor, TimeProvider clock)
    {
        _store = store;
        _api = api;
        _auth = auth;
        _roles = roles;
        _monitor = monitor;
        _clock = clock;
    }

    /// <summary>Raised when the data changed (downloaded, loaded from the device or wiped).</summary>
    public event Action? Changed;

    public LookupSnapshot<ProductLookupDto>? Products { get; private set; }

    public LookupSnapshot<ClientLookupDto>? Clients { get; private set; }

    /// <summary>Oldest confirmation of the two lists; null until both are on the device.</summary>
    public DateTimeOffset? CheckedAt =>
        Products is not null && Clients is not null ? (Products.CheckedAt < Clients.CheckedAt ? Products.CheckedAt : Clients.CheckedAt) : null;

    /// <summary>Starts following login, role and connection changes, loads what the device holds and refreshes it.</summary>
    public async Task StartAsync()
    {
        if (!_started)
        {
            _started = true;
            _auth.SessionChanged += OnSessionChanged;
            _roles.ActiveRoleChanged += OnActiveRoleChanged;
            _monitor.QualityChanged += OnQualityChanged;
        }

        await RefreshAsync();
    }

    /// <summary>Reads the lists kept on the device for the signed-in worker (once per worker).</summary>
    public async Task LoadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await LoadCoreAsync();
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>
    /// Downloads what changed, when the worker is working in a sales role and the device is not offline.
    /// False when nothing could be checked (offline, another role, or the request failed).
    /// </summary>
    public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!await LoadCoreAsync() || !OfflineRoles.Contains(await _roles.LoadAsync() ?? string.Empty)
                || _monitor.Quality == ConnectionQuality.Offline)
            {
                return false;
            }

            var products = await RefreshListAsync(ProductsUrl, ProductsKey, Products, cancellationToken);
            var clients = await RefreshListAsync(ClientsUrl, ClientsKey, Clients, cancellationToken);
            (Products, Clients) = (products ?? Products, clients ?? Clients);
            Changed?.Invoke();
            return products is not null && clients is not null;
        }
        finally
        {
            _lock.Release();
        }
    }

    /// <summary>Wipes everything the app keeps on the device (sign-out).</summary>
    public async Task ForgetAsync()
    {
        await _lock.WaitAsync();
        try
        {
            await _store.ClearAsync();
            ClearMemory();
        }
        finally
        {
            _lock.Release();
        }
    }

    public void Dispose()
    {
        if (_started)
        {
            _auth.SessionChanged -= OnSessionChanged;
            _roles.ActiveRoleChanged -= OnActiveRoleChanged;
            _monitor.QualityChanged -= OnQualityChanged;
        }

        _lock.Dispose();
    }

    /// <summary>False when nobody is signed in.</summary>
    private async Task<bool> LoadCoreAsync()
    {
        var user = await _auth.GetCurrentUserAsync();
        if (user is null)
        {
            return false;
        }

        if (_loadedFor == user.Id)
        {
            return true;
        }

        var owner = await _store.GetAsync<Guid?>(LocalStoreName.Meta, OwnerKey);
        if (owner != user.Id)
        {
            // Data (or queued orders) of another worker who did not sign out: not theirs to see or send.
            await _store.ClearAsync();
            await _store.PutAsync<Guid?>(LocalStoreName.Meta, OwnerKey, user.Id);
        }

        Products = await _store.GetAsync<LookupSnapshot<ProductLookupDto>>(LocalStoreName.Lookup, ProductsKey);
        Clients = await _store.GetAsync<LookupSnapshot<ClientLookupDto>>(LocalStoreName.Lookup, ClientsKey);
        _loadedFor = user.Id;
        Changed?.Invoke();
        return true;
    }

    private async Task<LookupSnapshot<T>?> RefreshListAsync<T>(string url, string key, LookupSnapshot<T>? current, CancellationToken cancellationToken)
    {
        var result = await _api.GetIfChangedAsync<List<T>>(url, current?.ETag, cancellationToken);
        if (!result.IsSuccess)
        {
            return null;
        }

        var snapshot = result.Value.NotModified && current is not null
            ? current with { CheckedAt = _clock.GetUtcNow() }
            : new LookupSnapshot<T>(result.Value.Value ?? [], result.Value.ETag, _clock.GetUtcNow());
        await _store.PutAsync(LocalStoreName.Lookup, key, snapshot);
        return snapshot;
    }

    private void ClearMemory()
    {
        (Products, Clients, _loadedFor) = (null, null, null);
        Changed?.Invoke();
    }

    private async void OnSessionChanged()
    {
        try
        {
            if (await _auth.GetCurrentUserAsync() is null)
            {
                // Expired or ended elsewhere: the client data goes, queued orders stay for the same worker.
                await _store.ClearAsync(LocalStoreName.Lookup);
                ClearMemory();
                return;
            }

            await RefreshAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    private async void OnActiveRoleChanged()
    {
        try
        {
            await RefreshAsync();
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
            await RefreshAsync();
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
