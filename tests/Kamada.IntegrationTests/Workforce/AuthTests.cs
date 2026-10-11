using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Workforce;
using ContractRole = Kamada.Shared.Contracts.Workforce.WorkerRole;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed class AuthTests(KamadaApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokenPairAndUser()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);

        var tokens = await _client.LoginAsync(worker.Username.ToUpperInvariant(), worker.Password);

        tokens.AccessToken.ShouldNotBeNullOrWhiteSpace();
        tokens.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        tokens.User.Id.ShouldBe(worker.Id);
        tokens.User.Roles.ShouldBe([Roles.Courier]);
        tokens.AccessTokenExpiresAt.ShouldBeLessThan(tokens.RefreshTokenExpiresAt);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401Problem()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, "Wrong1234!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_UnknownUser_ReturnsSameErrorAsWrongPassword()
    {
        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest("nobody-here", "Wrong1234!"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_InactiveWorker_Returns401()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier, isActive: false);

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithEmptyFields_Returns400ValidationProblem()
    {
        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest("", ""));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldContain("Username");
    }

    [Fact]
    public async Task Refresh_RotatesToken_AndReuseOfRotatedTokenRevokesWholeFamily()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var first = await _client.LoginAsync(worker.Username, worker.Password);

        var refreshed = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        refreshed.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await refreshed.ReadAsAsync<TokenResponse>();
        second.RefreshToken.ShouldNotBe(first.RefreshToken);

        var reuse = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(first.RefreshToken));
        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var afterReuse = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(second.RefreshToken));
        afterReuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }


    [Fact]
    public async Task Login_WorkerWithSeveralRoles_CanCallEndpointsOfEachRole()
    {
        var worker = await factory.CreateWorkerAsync([WorkerRole.Admin, WorkerRole.Courier]);
        var tokens = await _client.LoginAsync(worker.Username, worker.Password);
        _client.WithBearer(tokens.AccessToken);

        tokens.User.Roles.ShouldBe([Roles.Admin, Roles.Courier]);
        (await _client.GetAsync("/api/workers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _client.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_WorkerWithSeveralRoles_KeepsAllRoles()
    {
        var worker = await factory.CreateWorkerAsync([WorkerRole.Admin, WorkerRole.Courier]);
        var tokens = await _client.LoginAsync(worker.Username, worker.Password);

        var refreshed = await (await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken)))
            .ReadAsAsync<TokenResponse>();

        refreshed.User.Roles.ShouldBe([Roles.Admin, Roles.Courier]);
        _client.WithBearer(refreshed.AccessToken);
        (await _client.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_AfterAdminChangedRoles_IssuesTokenWithNewRoles()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var tokens = await _client.LoginAsync(worker.Username, worker.Password);

        (await admin.PutJsonAsync($"/api/workers/{worker.Id}", new UpdateWorkerRequest("Test", "Courier", "+48 600 000 000", [ContractRole.Admin])))
            .EnsureSuccessStatusCode();

        // The access token issued before the change keeps the old roles until it is refreshed.
        _client.WithBearer(tokens.AccessToken);
        (await _client.GetAsync("/api/workers", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        var refreshed = await (await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken)))
            .ReadAsAsync<TokenResponse>();
        refreshed.User.Roles.ShouldBe([Roles.Admin]);
        _client.WithBearer(refreshed.AccessToken);
        (await _client.GetAsync("/api/workers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var tokens = await _client.LoginAsync(worker.Username, worker.Password);
        _client.WithBearer(tokens.AccessToken);

        var logout = await _client.PostJsonAsync("/api/auth/logout", new LogoutRequest(tokens.RefreshToken));
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refresh = await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken));
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithAccessToken_ReturnsCurrentWorker()
    {
        var (client, worker) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        var response = await client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<AuthenticatedUserDto>()).Username.ShouldBe(worker.Username);
    }

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await _client.GetAsync("/api/auth/me", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
