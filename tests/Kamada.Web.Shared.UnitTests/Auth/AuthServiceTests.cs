using System.Text.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;

namespace Kamada.Web.Shared.UnitTests.Auth;

public sealed class AuthServiceTests : IDisposable
{
    private const string StorageKey = "kamada.test.session";

    private readonly FakeJsRuntime _js = new();
    private readonly StubApi _api = new();
    private readonly AuthService _auth;

    public AuthServiceTests()
    {
        var options = new KamadaClientOptions
        {
            ApiBaseUrl = new Uri("http://api.test/"),
            AllowedRoles = [Roles.Courier],
            StorageKey = StorageKey,
        };
        _auth = new AuthService(_api, new TokenStore(_js, options), options, TimeProvider.System);
    }

    [Fact]
    public async Task LoginAsync_UserWithAnAllowedRoleAmongOthers_SignsIn()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Admin, Roles.Courier);

        var result = await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        (await _auth.GetCurrentUserAsync())!.Roles.ShouldBe([Roles.Admin, Roles.Courier]);
        _js.LocalStorage.ShouldContainKey(StorageKey);
    }

    [Fact]
    public async Task LoginAsync_UserWithoutAllowedRole_FailsAndRevokesSession()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Admin);

        var result = await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Problem!.Status.ShouldBe(403);
        _api.RequestedPaths.ShouldContain("/api/auth/logout");
        (await _auth.GetCurrentUserAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_RolesNoLongerAllowed_SignsOut()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Courier);
        var login = await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        _api.RefreshResponse = StubApi.Tokens(Roles.Admin);
        var sessionChanged = false;
        _auth.SessionChanged += () => sessionChanged = true;

        var accessToken = await _auth.RefreshAsync(staleAccessToken: GetStoredAccessToken(), TestContext.Current.CancellationToken);

        login.IsSuccess.ShouldBeTrue();
        accessToken.ShouldBeNull();
        sessionChanged.ShouldBeTrue();
        (await _auth.GetCurrentUserAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task RefreshAsync_RolesChanged_KeepsSessionAndRaisesSessionChanged()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Courier);
        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        _api.RefreshResponse = StubApi.Tokens(Roles.Admin, Roles.Courier);
        var sessionChanged = false;
        _auth.SessionChanged += () => sessionChanged = true;

        var accessToken = await _auth.RefreshAsync(staleAccessToken: GetStoredAccessToken(), TestContext.Current.CancellationToken);

        accessToken.ShouldBe(_api.RefreshResponse.AccessToken);
        sessionChanged.ShouldBeTrue();
        (await _auth.GetCurrentUserAsync())!.Roles.ShouldBe([Roles.Admin, Roles.Courier]);
    }

    [Fact]
    public async Task GetCurrentUserAsync_SessionSavedWithSingleRole_IsSignedOut()
    {
        var tokens = StubApi.Tokens(Roles.Courier);
        var json = JsonSerializer.Serialize(tokens, KamadaJson.Options).Replace("\"roles\":[\"Courier\"]", "\"role\":\"Courier\"");
        _js.LocalStorage[StorageKey] = json;

        (await _auth.GetCurrentUserAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task GetAuthenticationStateAsync_UserWithSeveralRoles_HasEveryRole()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Admin, Roles.Courier);
        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        using var provider = new SessionAuthenticationStateProvider(_auth);

        var state = await provider.GetAuthenticationStateAsync();

        state.User.IsInRole(Roles.Admin).ShouldBeTrue();
        state.User.IsInRole(Roles.Courier).ShouldBeTrue();
    }

    public void Dispose()
    {
        _auth.Dispose();
        _api.Dispose();
    }

    private string GetStoredAccessToken() =>
        JsonSerializer.Deserialize<TokenResponse>(_js.LocalStorage[StorageKey], KamadaJson.Options)!.AccessToken;
}
