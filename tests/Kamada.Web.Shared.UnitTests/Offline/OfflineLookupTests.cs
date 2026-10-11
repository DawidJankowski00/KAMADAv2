using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;
using Kamada.Web.Shared.Connectivity;
using Kamada.Web.Shared.Offline;
using Kamada.Web.Shared.UnitTests.Auth;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Web.Shared.UnitTests.Offline;

public sealed class OfflineLookupTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-10-11T08:00:00Z"));
    private readonly FakeJsRuntime _js = new();
    private readonly StubApi _authApi = new();
    private readonly LookupServer _server = new();
    private readonly FakeLocalStore _store = new();
    private readonly KamadaClientOptions _options = new()
    {
        ApiBaseUrl = new Uri("http://api.test/"),
        AllowedRoles = [Roles.Seller, Roles.Courier, Roles.Admin],
        StorageKey = "kamada.test.session",
    };

    private readonly AuthService _auth;
    private readonly RoleContext _roles;
    private readonly ConnectionMonitor _monitor;
    private readonly List<OfflineLookup> _created = [];

    public OfflineLookupTests()
    {
        _auth = new AuthService(_authApi, new TokenStore(_js, _options), _options, _clock);
        _roles = new RoleContext(_auth, _js, _options);
        _monitor = new ConnectionMonitor(_authApi, _clock);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RefreshAsync_Seller_DownloadsAndKeepsBothLists()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();

        (await lookup.RefreshAsync(Ct)).ShouldBeTrue();

        lookup.Products!.Items.Select(p => p.Sku).ShouldBe(["WODA-05"]);
        lookup.Clients!.Items.Single().Id.ShouldBe(_server.Clients.Single().Id);
        lookup.CheckedAt.ShouldBe(_clock.GetUtcNow());
        _store.Holds(LocalStoreName.Lookup).ShouldBeTrue();
    }

    [Fact]
    public async Task RefreshAsync_NothingChanged_SendsTagAndKeepsTheLists()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();
        await lookup.RefreshAsync(Ct);
        _clock.Advance(TimeSpan.FromMinutes(10));

        (await lookup.RefreshAsync(Ct)).ShouldBeTrue();

        _server.Requests.TakeLast(2).ShouldAllBe(r => r.Status == System.Net.HttpStatusCode.NotModified && r.IfNoneMatch != null);
        lookup.Products!.Items.ShouldHaveSingleItem();
        lookup.CheckedAt.ShouldBe(_clock.GetUtcNow());
    }

    [Fact]
    public async Task RefreshAsync_DataChanged_DownloadsTheNewVersion()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();
        await lookup.RefreshAsync(Ct);
        _server.Products.Add(new(Guid.NewGuid(), "SOK-1", "Sok jabłkowy", 6.20m, 12, []));
        _server.Change();

        await lookup.RefreshAsync(Ct);

        lookup.Products!.Items.Count.ShouldBe(2);
    }

    [Theory]
    [InlineData(Roles.Courier)]
    [InlineData(Roles.Admin)]
    public async Task RefreshAsync_RoleWithoutOfflineOrdering_DownloadsNothing(string role)
    {
        await SignInAsync(role);
        var lookup = NewLookup();

        (await lookup.RefreshAsync(Ct)).ShouldBeFalse();

        _server.Requests.ShouldBeEmpty();
        lookup.Products.ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_Offline_KeepsWhatTheDeviceHas()
    {
        await SignInAsync(Roles.Seller);
        await NewLookup().RefreshAsync(Ct);
        await _monitor.SetBrowserOnline(false);
        var restarted = NewLookup();

        (await restarted.RefreshAsync(Ct)).ShouldBeFalse();

        restarted.Products!.Items.ShouldHaveSingleItem();
        _server.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task RefreshAsync_ServerUnreachable_KeepsWhatTheDeviceHas()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();
        await lookup.RefreshAsync(Ct);
        _server.Unreachable = true;

        (await lookup.RefreshAsync(Ct)).ShouldBeFalse();

        lookup.Clients!.Items.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task LoadAsync_AfterRestart_ReadsTheListsFromTheDevice()
    {
        await SignInAsync(Roles.Seller);
        await NewLookup().RefreshAsync(Ct);
        var restarted = NewLookup();

        await restarted.LoadAsync();

        restarted.Products!.Items.Select(p => p.Sku).ShouldBe(["WODA-05"]);
        _server.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task LoadAsync_AnotherWorkerSignedIn_StartsFromAnEmptyDevice()
    {
        await SignInAsync(Roles.Seller);
        await NewLookup().RefreshAsync(Ct);
        await _store.PutAsync(LocalStoreName.Outbox, "order-1", "queued");
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();

        await lookup.LoadAsync();

        lookup.Products.ShouldBeNull();
        _store.Holds(LocalStoreName.Outbox).ShouldBeFalse();
    }

    [Fact]
    public async Task ForgetAsync_WipesEverything()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();
        await lookup.RefreshAsync(Ct);
        await _store.PutAsync(LocalStoreName.Outbox, "order-1", "queued");

        await lookup.ForgetAsync();

        _store.Values.ShouldBeEmpty();
        lookup.Products.ShouldBeNull();
        lookup.Clients.ShouldBeNull();
    }

    [Fact]
    public async Task SessionEnded_WipesTheListsButKeepsQueuedOrders()
    {
        await SignInAsync(Roles.Seller);
        var lookup = NewLookup();
        await lookup.StartAsync();
        await _store.PutAsync(LocalStoreName.Outbox, "order-1", "queued");

        // An expired session: the refresh token is rejected.
        _authApi.RefreshResponse = null;
        _clock.Advance(TimeSpan.FromMinutes(20));
        await _auth.GetAccessTokenAsync(Ct);

        await WaitUntilAsync(() => lookup.Products is null);
        _store.Holds(LocalStoreName.Lookup).ShouldBeFalse();
        _store.Holds(LocalStoreName.Outbox).ShouldBeTrue();
    }

    [Fact]
    public async Task ActiveRoleChanged_ToSeller_Downloads()
    {
        await SignInAsync(Roles.Courier, Roles.Seller);
        await _roles.SwitchToAsync(Roles.Courier);
        var lookup = NewLookup();
        await lookup.StartAsync();
        _server.Requests.ShouldBeEmpty();

        await _roles.SwitchToAsync(Roles.Seller);

        await WaitUntilAsync(() => lookup.Products is not null);
    }

    public void Dispose()
    {
        foreach (var lookup in _created)
        {
            lookup.Dispose();
        }

        _roles.Dispose();
        _monitor.Dispose();
        _auth.Dispose();
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        for (var i = 0; i < 100 && !condition(); i++)
        {
            await Task.Delay(10, Ct);
        }

        condition().ShouldBeTrue();
    }

    private OfflineLookup NewLookup()
    {
        var lookup = new OfflineLookup(_store, new ApiClient(_server.CreateClient()), _auth, _roles, _monitor, _clock);
        _created.Add(lookup);
        return lookup;
    }

    private async Task SignInAsync(params string[] roles)
    {
        _authApi.LoginResponse = StubApi.Tokens(roles) with
        {
            AccessTokenExpiresAt = _clock.GetUtcNow().AddMinutes(15),
            RefreshTokenExpiresAt = _clock.GetUtcNow().AddDays(7),
        };
        (await _auth.LoginAsync("anna", "secret", Ct)).IsSuccess.ShouldBeTrue();
    }
}
