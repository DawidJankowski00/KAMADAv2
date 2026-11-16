using Kamada.Api.Extensions;
using Kamada.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kamada.IntegrationTests;

public sealed class CorsTests(KamadaApiFactory factory) : IAsyncDisposable
{
    private const string Panel = "https://panel.kamada.test";

    // A trailing slash in the configuration (as in .env) must still match the browser's Origin header.
    private readonly WebApplicationFactory<Program> _host =
        factory.WithWebHostBuilder(builder => builder.UseSetting("Cors:AllowedOrigins:0", Panel + "/"));

    [Fact]
    public async Task Preflight_FromAConfiguredOrigin_IsAllowed()
    {
        var response = await _host.CreateClient().SendAsync(Preflight(Panel, "PUT"), TestContext.Current.CancellationToken);

        response.Headers.GetValues("Access-Control-Allow-Origin").ShouldBe([Panel]);
        response.Headers.Contains("Access-Control-Allow-Credentials").ShouldBeFalse();
    }

    [Fact]
    public async Task Preflight_FromAnotherSite_GetsNoCorsHeaders()
    {
        var response = await _host.CreateClient().SendAsync(Preflight("https://evil.example", "POST"), TestContext.Current.CancellationToken);

        response.Headers.Contains("Access-Control-Allow-Origin").ShouldBeFalse();
    }

    [Fact]
    public async Task Preflight_ForAMethodTheAppsDoNotUse_IsNotAllowed()
    {
        var response = await _host.CreateClient().SendAsync(Preflight(Panel, "PATCH"), TestContext.Current.CancellationToken);

        // The browser compares the request with the advertised methods and refuses PATCH itself.
        string.Join(",", response.Headers.GetValues("Access-Control-Allow-Methods")).ShouldNotContain("PATCH");
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://panel.kamada.test/app")]
    [InlineData("panel.kamada.test")]
    public void Normalize_RejectsAnythingButAnOrigin(string entry)
    {
        new CorsSettings { AllowedOrigins = [entry] }.Normalize().Invalid.ShouldBe(entry);
    }

    private static HttpRequestMessage Preflight(string origin, string method)
    {
        var request = new HttpRequestMessage(HttpMethod.Options, "/api/products");
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "authorization");
        return request;
    }

    public ValueTask DisposeAsync() => _host.DisposeAsync();
}
