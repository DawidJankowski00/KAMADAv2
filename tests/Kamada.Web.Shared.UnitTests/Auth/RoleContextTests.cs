using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Auth;

namespace Kamada.Web.Shared.UnitTests.Auth;

public sealed class RoleContextTests : IDisposable
{
    private const string Seller = "Seller";

    private readonly FakeJsRuntime _js = new();
    private readonly StubApi _api = new();
    private readonly AuthService _auth;
    private readonly KamadaClientOptions _options;
    private string? _accessToken;

    public RoleContextTests()
    {
        _options = new KamadaClientOptions
        {
            ApiBaseUrl = new Uri("http://api.test/"),
            AllowedRoles = [Seller, Roles.Courier, Roles.Admin],
            StorageKey = "kamada.test.session",
        };
        _auth = new AuthService(_api, new TokenStore(_js, _options), _options, TimeProvider.System);
    }

    [Fact]
    public async Task LoadAsync_SignedOut_HasNoActiveRole()
    {
        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBeNull();
        roles.AvailableRoles.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadAsync_SingleRole_ActivatesIt()
    {
        await SignInAsync(Roles.Admin);
        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBe(Roles.Admin);
        roles.AvailableRoles.ShouldBe([Roles.Admin]);
    }

    [Fact]
    public async Task LoadAsync_SeveralRolesNothingRemembered_ActivatesFirstInAppOrder()
    {
        await SignInAsync(Roles.Admin, Roles.Courier, Seller);
        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBe(Seller);
        roles.AvailableRoles.ShouldBe([Seller, Roles.Courier, Roles.Admin]);
    }

    [Fact]
    public async Task LoadAsync_RoleTheAppDoesNotSupport_IsNotAvailable()
    {
        await SignInAsync(Roles.Admin, "Accountant");
        using var roles = NewRoleContext();

        await roles.LoadAsync();

        roles.AvailableRoles.ShouldBe([Roles.Admin]);
    }

    [Fact]
    public async Task LoadAsync_RoleRememberedEarlier_ActivatesIt()
    {
        await SignInAsync(Roles.Admin, Roles.Courier);
        using (var earlier = NewRoleContext())
        {
            (await earlier.SwitchToAsync(Roles.Admin)).ShouldBeTrue();
        }

        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBe(Roles.Admin);
    }

    [Fact]
    public async Task LoadAsync_RememberedRoleTakenAway_FallsBackToAppOrder()
    {
        var user = await SignInAsync(Roles.Admin, Roles.Courier);
        using (var earlier = NewRoleContext())
        {
            await earlier.SwitchToAsync(Roles.Admin);
        }

        await SignInAsync(user with { Roles = [Roles.Courier, Seller] });
        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBe(Seller);
    }

    [Fact]
    public async Task SwitchToAsync_AvailableRole_ChangesActiveRoleAndRaisesEvent()
    {
        await SignInAsync(Roles.Admin, Roles.Courier);
        using var roles = NewRoleContext();
        await roles.LoadAsync();
        var changed = 0;
        roles.ActiveRoleChanged += () => changed++;

        var switched = await roles.SwitchToAsync(Roles.Admin);

        switched.ShouldBeTrue();
        roles.ActiveRole.ShouldBe(Roles.Admin);
        changed.ShouldBe(1);
    }

    [Fact]
    public async Task SwitchToAsync_RoleTheUserDoesNotHave_ReturnsFalse()
    {
        await SignInAsync(Roles.Courier);
        using var roles = NewRoleContext();
        await roles.LoadAsync();

        var switched = await roles.SwitchToAsync(Roles.Admin);

        switched.ShouldBeFalse();
        roles.ActiveRole.ShouldBe(Roles.Courier);
    }

    [Fact]
    public async Task SwitchToAsync_RememberedPerUser_DoesNotAffectAnotherUser()
    {
        await SignInAsync(Roles.Admin, Roles.Courier);
        using (var first = NewRoleContext())
        {
            await first.SwitchToAsync(Roles.Admin);
        }

        await SignInAsync(Roles.Admin, Roles.Courier);
        using var roles = NewRoleContext();

        var active = await roles.LoadAsync();

        active.ShouldBe(Roles.Courier);
    }

    [Fact]
    public async Task SessionChanged_Logout_ClearsActiveRoleAndRaisesEvent()
    {
        await SignInAsync(Roles.Courier);
        using var roles = NewRoleContext();
        await roles.LoadAsync();
        var changed = 0;
        roles.ActiveRoleChanged += () => changed++;

        await _auth.LogoutAsync(TestContext.Current.CancellationToken);

        roles.ActiveRole.ShouldBeNull();
        roles.AvailableRoles.ShouldBeEmpty();
        changed.ShouldBe(1);
    }

    [Fact]
    public async Task SessionChanged_ActiveRoleRemovedAtRefresh_SwitchesToRemainingRole()
    {
        var user = await SignInAsync(Roles.Admin, Roles.Courier);
        using var roles = NewRoleContext();
        await roles.SwitchToAsync(Roles.Admin);
        _api.RefreshResponse = StubApi.Tokens(Roles.Courier) with { User = user with { Roles = [Roles.Courier] } };

        await _auth.RefreshAsync(staleAccessToken: _accessToken!, TestContext.Current.CancellationToken);

        roles.ActiveRole.ShouldBe(Roles.Courier);
        roles.AvailableRoles.ShouldBe([Roles.Courier]);
    }

    public void Dispose()
    {
        _auth.Dispose();
        _api.Dispose();
    }

    private RoleContext NewRoleContext() => new(_auth, _js, _options);

    private async Task<AuthenticatedUserDto> SignInAsync(params string[] roles)
    {
        var tokens = StubApi.Tokens(roles);
        await SignInAsync(tokens.User);
        return tokens.User;
    }

    private async Task SignInAsync(AuthenticatedUserDto user)
    {
        _api.LoginResponse = StubApi.Tokens() with { User = user };
        _accessToken = _api.LoginResponse.AccessToken;
        (await _auth.LoginAsync(user.Username, "secret", TestContext.Current.CancellationToken)).IsSuccess.ShouldBeTrue();
    }
}
