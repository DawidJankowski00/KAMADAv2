using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed class SessionTests(KamadaApiFactory factory)
{
    private const string Laptop = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0";
    private const string Phone = "Mozilla/5.0 (Linux; Android 15) Chrome/140.0 Mobile";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Sessions_ListsEachDeviceWithTheCurrentOneFirst()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        await SignInAsync(worker, Phone);
        var (laptop, _) = await SignInAsync(worker, Laptop);

        var sessions = await (await laptop.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>();

        sessions.Count.ShouldBe(2);
        sessions[0].IsCurrent.ShouldBeTrue();
        sessions[0].UserAgent.ShouldBe(Laptop);
        sessions[1].IsCurrent.ShouldBeFalse();
        sessions[1].UserAgent.ShouldBe(Phone);
        sessions.ShouldAllBe(s => s.StartedAt <= s.LastUsedAt); // TestServer has no remote IP, so IpAddress stays empty here
    }

    [Fact]
    public async Task Sessions_RefreshKeepsTheSessionAndUpdatesLastUse()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var (laptop, tokens) = await SignInAsync(worker, Laptop);
        var before = (await (await laptop.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>()).Single();

        var refreshed = await (await factory.CreateClient().PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken)))
            .ReadAsAsync<TokenResponse>();
        var after = (await (await factory.CreateClient().WithBearer(refreshed.AccessToken).GetAsync("/api/auth/sessions", Ct))
            .ReadAsAsync<List<SessionDto>>()).Single();

        after.Id.ShouldBe(before.Id);
        after.StartedAt.ShouldBe(before.StartedAt);
        after.LastUsedAt.ShouldBeGreaterThan(before.LastUsedAt);
        after.IsCurrent.ShouldBeTrue();
    }

    [Fact]
    public async Task RevokeSession_SignsThatDeviceOut()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Seller);
        var (_, phoneTokens) = await SignInAsync(worker, Phone);
        var (laptop, _) = await SignInAsync(worker, Laptop);
        var phoneSession = (await (await laptop.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>()).Single(s => !s.IsCurrent);

        var response = await laptop.DeleteAsync($"/api/auth/sessions/{phoneSession.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await factory.CreateClient().PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(phoneTokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await (await laptop.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>()).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RevokeSession_OfAnotherWorker_Returns404()
    {
        var other = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var (otherClient, _) = await SignInAsync(other, Phone);
        var otherSession = (await (await otherClient.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>()).Single();
        var (client, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        var response = await client.DeleteAsync($"/api/auth/sessions/{otherSession.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.SessionNotFound");
    }

    [Fact]
    public async Task RevokeOthers_KeepsOnlyTheCurrentSession()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var (_, phoneTokens) = await SignInAsync(worker, Phone);
        await SignInAsync(worker, Phone);
        var (laptop, laptopTokens) = await SignInAsync(worker, Laptop);

        (await laptop.PostAsync("/api/auth/sessions/revoke-others", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var sessions = await (await laptop.GetAsync("/api/auth/sessions", Ct)).ReadAsAsync<List<SessionDto>>();
        sessions.Single().IsCurrent.ShouldBeTrue();
        (await factory.CreateClient().PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(phoneTokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await factory.CreateClient().PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(laptopTokens.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private async Task<(HttpClient Client, TokenResponse Tokens)> SignInAsync(TestWorker worker, string userAgent)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(userAgent);
        var tokens = await client.LoginAsync(worker.Username, worker.Password);
        return (client.WithBearer(tokens.AccessToken), tokens);
    }
}
