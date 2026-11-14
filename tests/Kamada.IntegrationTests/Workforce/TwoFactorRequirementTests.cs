using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OtpNet;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

/// <summary>Auth:TwoFactor:RequiredForAdmins on (the default outside Development), in a host of its own.</summary>
public sealed class TwoFactorRequirementTests(KamadaApiFactory factory) : IAsyncDisposable
{
    private readonly WebApplicationFactory<Program> _host =
        factory.WithWebHostBuilder(builder => builder.UseSetting("Auth:TwoFactor:RequiredForAdmins", "true"));

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdminWithoutTwoFactor_CanUseOnlyAccountEndpoints()
    {
        var admin = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var tokens = await _host.CreateClient().LoginAsync(admin.Username, admin.Password);
        var client = _host.CreateClient().WithBearer(tokens.AccessToken);

        tokens.User.TwoFactorSetupRequired.ShouldBeTrue();
        var workers = await client.GetAsync("/api/workers", Ct);
        workers.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await workers.ReadProblemCodeAsync()).ShouldBe("Auth.TwoFactorSetupRequired");
        (await client.GetAsync("/api/auth/two-factor", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/api/auth/sessions", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminAfterEnablingTwoFactor_GetsFullAccessWithTheNextTokens()
    {
        var admin = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var tokens = await _host.CreateClient().LoginAsync(admin.Username, admin.Password);
        var client = _host.CreateClient().WithBearer(tokens.AccessToken);
        var setup = await (await client.PostAsync("/api/auth/two-factor/setup", null, Ct)).ReadAsAsync<TwoFactorSetupDto>();
        var totp = new Totp(Base32Encoding.ToBytes(setup.SharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));
        (await client.PostJsonAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest(totp.ComputeTotp(DateTime.UtcNow))))
            .EnsureSuccessStatusCode();

        var refreshed = await (await _host.CreateClient().PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(tokens.RefreshToken)))
            .ReadAsAsync<TokenResponse>();

        refreshed.User.TwoFactorSetupRequired.ShouldBeFalse();
        (await _host.CreateClient().WithBearer(refreshed.AccessToken).GetAsync("/api/workers", Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task CourierWithoutTwoFactor_IsNotAffected()
    {
        var courier = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var tokens = await _host.CreateClient().LoginAsync(courier.Username, courier.Password);

        tokens.User.TwoFactorSetupRequired.ShouldBeFalse();
        (await _host.CreateClient().WithBearer(tokens.AccessToken).GetAsync("/api/courier/delivery-runs", Ct))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
