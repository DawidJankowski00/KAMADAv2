using Kamada.Web.Shared.Platform;
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
        _auth = new AuthService(_api, new TokenStore(new BrowserSessionStorage(_js), options), options, TimeProvider.System);
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
    public async Task LogoutAsync_ByTheWorker_IsRememberedUntilTheNextSignIn()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Courier);
        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        _auth.SignedOutByUser.ShouldBeFalse();

        await _auth.LogoutAsync(TestContext.Current.CancellationToken);
        _auth.SignedOutByUser.ShouldBeTrue();

        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        _auth.SignedOutByUser.ShouldBeFalse();
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

    [Fact]
    public async Task ChangePasswordAsync_Success_StoresTheNewSession()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Courier);
        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);
        _api.ChangePasswordResponse = StubApi.Tokens(Roles.Courier);

        var result = await _auth.ChangePasswordAsync("secret", "Nowe-haslo-do-testow", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        GetStoredAccessToken().ShouldBe(_api.ChangePasswordResponse.AccessToken);
    }

    [Fact]
    public async Task ChangePasswordAsync_WrongCurrentPassword_KeepsSessionAndReturnsProblem()
    {
        _api.LoginResponse = StubApi.Tokens(Roles.Courier);
        await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);

        var result = await _auth.ChangePasswordAsync("wrong", "Nowe-haslo-do-testow", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        result.Problem!.Code.ShouldBe("Auth.CurrentPasswordInvalid");
        GetStoredAccessToken().ShouldBe(_api.LoginResponse.AccessToken);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_Accepted_SucceedsWithoutSession()
    {
        var result = await _auth.RequestPasswordResetAsync("anna@kamada.test", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        _api.RequestedPaths.ShouldBe(["/api/auth/forgot-password"]);
    }

    [Fact]
    public async Task LoginAsync_AccountWithTwoFactor_ReturnsTheChallengeWithoutSigningIn()
    {
        _api.LoginChallenge = new TwoFactorChallengeDto("challenge", DateTimeOffset.UtcNow.AddMinutes(5));

        var result = await _auth.LoginAsync("anna", "secret", TestContext.Current.CancellationToken);

        result.Value.NeedsCode.ShouldBeTrue();
        (await _auth.GetCurrentUserAsync()).ShouldBeNull();
    }

    [Fact]
    public async Task CompleteTwoFactorLoginAsync_ValidCode_SignsIn()
    {
        _api.TwoFactorResponse = StubApi.Tokens(Roles.Courier);

        var result = await _auth.CompleteTwoFactorLoginAsync("challenge", "123456", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        GetStoredAccessToken().ShouldBe(_api.TwoFactorResponse.AccessToken);
    }

    [Fact]
    public async Task CompleteTwoFactorLoginAsync_WrongCode_StaysSignedOut()
    {
        var result = await _auth.CompleteTwoFactorLoginAsync("challenge", "000000", TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeFalse();
        (await _auth.GetCurrentUserAsync()).ShouldBeNull();
    }

    private string GetStoredAccessToken() =>
        JsonSerializer.Deserialize<TokenResponse>(_js.LocalStorage[StorageKey], KamadaJson.Options)!.AccessToken;
}
