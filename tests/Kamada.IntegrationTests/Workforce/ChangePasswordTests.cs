using System.Net;
using System.Net.Http.Headers;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed class ChangePasswordTests(KamadaApiFactory factory)
{
    private const string NewPassword = "Nowe-haslo-do-testow";

    [Fact]
    public async Task ChangePassword_WithCurrentPassword_ReturnsNewSessionAndRevokesOthers()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var otherDevice = await factory.CreateClient().LoginAsync(worker.Username, worker.Password);
        var client = await SignedInAsync(worker);

        var response = await client.PostJsonAsync("/api/auth/change-password", new ChangePasswordRequest(worker.Password, NewPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var tokens = await response.ReadAsAsync<TokenResponse>();
        tokens.User.Id.ShouldBe(worker.Id);

        var anonymous = factory.CreateClient();
        (await anonymous.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(otherDevice.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await anonymous.LoginAsync(worker.Username, NewPassword)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400AndKeepsPassword()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Seller);
        var client = await SignedInAsync(worker);

        var response = await client.PostJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Wrong1234!", NewPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.CurrentPasswordInvalid");
        (await factory.CreateClient().LoginAsync(worker.Username, worker.Password)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task ChangePassword_WithWeakNewPassword_Returns400()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var client = await SignedInAsync(worker);

        var response = await client.PostJsonAsync("/api/auth/change-password", new ChangePasswordRequest(worker.Password, "short"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        var response = await factory.CreateClient().PostJsonAsync("/api/auth/change-password", new ChangePasswordRequest("Old1234!", NewPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<HttpClient> SignedInAsync(TestWorker worker)
    {
        var client = factory.CreateClient();
        var tokens = await client.LoginAsync(worker.Username, worker.Password);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return client;
    }
}
