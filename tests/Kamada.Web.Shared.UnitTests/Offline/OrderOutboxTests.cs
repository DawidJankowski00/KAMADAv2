using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;
using Kamada.Web.Shared.Connectivity;
using Kamada.Web.Shared.Offline;
using Kamada.Web.Shared.UnitTests.Auth;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Web.Shared.UnitTests.Offline;

public sealed class OrderOutboxTests : IDisposable
{
    private static readonly Guid ProductId = Guid.NewGuid();

    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-10-11T08:00:00Z"));
    private readonly FakeJsRuntime _js = new();
    private readonly StubApi _authApi = new();
    private readonly OrderServer _server = new();
    private readonly FakeLocalStore _store = new();
    private readonly AuthService _auth;
    private readonly RoleContext _roles;
    private readonly ConnectionMonitor _monitor;
    private readonly OfflineLookup _lookup;
    private readonly List<OrderOutbox> _created = [];
    private readonly List<OrderDto> _sent = [];

    public OrderOutboxTests()
    {
        var options = new KamadaClientOptions
        {
            ApiBaseUrl = new Uri("http://api.test/"),
            AllowedRoles = [Roles.Seller],
            StorageKey = "kamada.test.session",
        };
        _auth = new AuthService(_authApi, new TokenStore(_js, options), options, _clock);
        _roles = new RoleContext(_auth, _js, options);
        _monitor = new ConnectionMonitor(_authApi, _clock);
        _lookup = new OfflineLookup(_store, new ApiClient(new LookupServer().CreateClient()), _auth, _roles, _monitor, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SendAsync_ExistingClient_PlacesTheOrderWithItsRequestIdAndRemovesIt()
    {
        var outbox = await StartAsync();
        var entry = Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid());
        await outbox.EnqueueAsync(entry);

        (await outbox.SendAsync(Ct)).ShouldBe(1);

        var (_, path, body) = _server.Requests.ShouldHaveSingleItem();
        path.ShouldBe("/api/orders");
        body.GetProperty("requestId").GetGuid().ShouldBe(entry.Id);
        body.GetProperty("clientId").GetGuid().ShouldBe(entry.ClientId!.Value);
        outbox.Entries.ShouldBeEmpty();
        _store.Holds(LocalStoreName.Outbox).ShouldBeFalse();
        _sent.ShouldHaveSingleItem().Number.ShouldBe(100);
    }

    [Fact]
    public async Task SendAsync_NewClient_CreatesTheClientThenPlacesTheOrderForIt()
    {
        var outbox = await StartAsync();
        await outbox.EnqueueAsync(Entry(newClient: NewClient()));

        await outbox.SendAsync(Ct);

        _server.Paths.ShouldBe(["/api/clients", "/api/orders"]);
        var client = _server.Requests[0].Body;
        var order = _server.Requests[1].Body;
        order.GetProperty("clientId").GetGuid().ShouldNotBe(Guid.Empty);
        client.GetProperty("requestId").GetGuid().ShouldNotBe(order.GetProperty("requestId").GetGuid());
        outbox.Entries.ShouldBeEmpty();
    }

    [Fact]
    public async Task SendAsync_NewAddress_AddsItToTheClientThenPlacesTheOrder()
    {
        var outbox = await StartAsync();
        var clientId = Guid.NewGuid();
        await outbox.EnqueueAsync(Entry(clientId: clientId, newAddress: NewAddress()));

        await outbox.SendAsync(Ct);

        _server.Paths.ShouldBe([$"/api/clients/{clientId}/addresses", "/api/orders"]);
    }

    [Fact]
    public async Task SendAsync_ConnectionLostAfterTheClientWasCreated_ResumesWithTheCreatedClient()
    {
        var outbox = await StartAsync();
        await outbox.EnqueueAsync(Entry(newClient: NewClient()));
        _server.Failures["/api/orders"] = new OrderServer.Failure(0);

        (await outbox.SendAsync(Ct)).ShouldBe(0);

        var waiting = outbox.Entries.ShouldHaveSingleItem();
        waiting.Status.ShouldBe(OutboxStatus.Pending);
        waiting.ClientId.ShouldNotBeNull();
        waiting.AddressId.ShouldNotBeNull();

        _server.Failures.Clear();
        (await outbox.SendAsync(Ct)).ShouldBe(1);
        _server.Paths.ShouldBe(["/api/clients", "/api/orders", "/api/orders"]);
    }

    [Fact]
    public async Task SendAsync_NotEnoughStock_NeedsAttentionAndTheNextOrderIsStillSent()
    {
        var outbox = await StartAsync();
        var first = Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid());
        await outbox.EnqueueAsync(first);
        _clock.Advance(TimeSpan.FromMinutes(1));
        await outbox.EnqueueAsync(Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid()));
        _server.RefuseOrdersFor = first.ClientId;

        (await outbox.SendAsync(Ct)).ShouldBe(1);

        var attention = outbox.Entries.ShouldHaveSingleItem();
        attention.Id.ShouldBe(first.Id);
        attention.Status.ShouldBe(OutboxStatus.NeedsAttention);
        attention.ProblemCode.ShouldBe("Products.InsufficientStock");
        attention.Problem.ShouldBe(ErrorMessages.TryTranslate("Products.InsufficientStock"));
        outbox.NeedsAttentionCount.ShouldBe(1);
    }

    [Fact]
    public async Task SendAsync_ServerUnavailable_StopsAndKeepsEveryOrder()
    {
        var outbox = await StartAsync();
        await outbox.EnqueueAsync(Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid()));
        _clock.Advance(TimeSpan.FromMinutes(1));
        await outbox.EnqueueAsync(Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid()));
        _server.Failures["/api/orders"] = new OrderServer.Failure(503);

        (await outbox.SendAsync(Ct)).ShouldBe(0);

        _server.Requests.Count.ShouldBe(1);
        outbox.Entries.Count.ShouldBe(2);
        outbox.Entries.ShouldAllBe(e => e.Status == OutboxStatus.Pending);
    }

    [Fact]
    public async Task UpdateAsync_EntryThatNeededAttention_IsSentAgain()
    {
        var outbox = await StartAsync();
        _server.Failures["/api/orders"] = new OrderServer.Failure(409, "Products.Inactive");
        var entry = Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid());
        await outbox.EnqueueAsync(entry);
        await outbox.SendAsync(Ct);
        _server.Failures.Clear();

        await outbox.UpdateAsync(outbox.Find(entry.Id)! with { Lines = [new OutboxLine(ProductId, "SOK-1", "Sok", 1, 6.20m)] });
        await outbox.SendAsync(Ct);

        outbox.Entries.ShouldBeEmpty();
        _server.Requests.Last().Body.GetProperty("items")[0].GetProperty("quantity").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task EnqueueAsync_Offline_KeepsTheOrderOnTheDeviceWithoutSending()
    {
        var outbox = await StartAsync();

        await outbox.EnqueueAsync(Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid()));

        _server.Requests.ShouldBeEmpty();
        _store.Holds(LocalStoreName.Outbox).ShouldBeTrue();
    }

    [Fact]
    public async Task StartAsync_AfterRestart_SendsWhatWasQueued()
    {
        var before = await StartAsync();
        await before.EnqueueAsync(Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid()));

        var restarted = NewOutbox();
        await restarted.StartAsync();

        restarted.Entries.ShouldBeEmpty();
        _server.Paths.ShouldBe(["/api/orders"]);
    }

    [Fact]
    public async Task DiscardAsync_RemovesTheEntryFromTheDevice()
    {
        var outbox = await StartAsync();
        var entry = Entry(clientId: Guid.NewGuid(), addressId: Guid.NewGuid());
        await outbox.EnqueueAsync(entry);

        (await outbox.DiscardAsync(entry.Id)).ShouldBeTrue();

        outbox.Entries.ShouldBeEmpty();
        _store.Holds(LocalStoreName.Outbox).ShouldBeFalse();
    }

    [Theory]
    [InlineData(0, null, true)]
    [InlineData(401, null, true)]
    [InlineData(503, null, true)]
    [InlineData(409, "Concurrency.Conflict", true)]
    [InlineData(409, "Products.InsufficientStock", false)]
    [InlineData(400, null, false)]
    [InlineData(404, "Clients.NotFound", false)]
    public void IsTransient_ClassifiesFailures(int status, string? code, bool transient) =>
        OrderOutbox.IsTransient(new ApiProblem(status, "x", Code: code)).ShouldBe(transient);

    public void Dispose()
    {
        foreach (var outbox in _created)
        {
            outbox.Dispose();
        }

        _lookup.Dispose();
        _roles.Dispose();
        _monitor.Dispose();
        _auth.Dispose();
    }

    private static CreateClientRequest NewClient() =>
        new("jan.nowy", "jan.nowy@example.com", "Jan", "Nowy", "+48 600 100 200", [NewAddress()], Guid.CreateVersion7());

    private static AddressRequest NewAddress() =>
        new(AddressType.Shipping, "Długa", "5", null, "Gdańsk", "80-001", RequestId: Guid.CreateVersion7());

    private OutboxEntry Entry(Guid? clientId = null, Guid? addressId = null, CreateClientRequest? newClient = null, AddressRequest? newAddress = null) => new(
        Guid.CreateVersion7(),
        _clock.GetUtcNow(),
        OutboxStatus.Pending,
        newClient,
        clientId,
        newAddress,
        addressId,
        new ClientLookupDto(clientId ?? Guid.Empty, "jan", "jan@example.com", "Jan", "Kowalski", "+48 600 000 000", []),
        "Długa 5, 80-001 Gdańsk",
        [new OutboxLine(ProductId, "WODA-05", "Woda 0,5 l", 6, 2.50m)],
        Notes: null);

    private async Task<OrderOutbox> StartAsync()
    {
        _authApi.LoginResponse = StubApi.Tokens(Roles.Seller) with
        {
            AccessTokenExpiresAt = _clock.GetUtcNow().AddDays(1),
            RefreshTokenExpiresAt = _clock.GetUtcNow().AddDays(7),
        };
        (await _auth.LoginAsync("anna", "secret", Ct)).IsSuccess.ShouldBeTrue();
        var outbox = NewOutbox();
        await outbox.StartAsync();

        // Queued orders are then sent only by explicit SendAsync calls (offline, nothing starts a run on its own).
        await _monitor.SetBrowserOnline(false);
        return outbox;
    }

    private OrderOutbox NewOutbox()
    {
        var outbox = new OrderOutbox(_store, new ApiClient(_server.CreateClient()), _auth, _monitor, _lookup, _clock);
        outbox.OrderSent += _sent.Add;
        _created.Add(outbox);
        return outbox;
    }
}
