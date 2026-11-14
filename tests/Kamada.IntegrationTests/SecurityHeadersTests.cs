using Kamada.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Kamada.IntegrationTests;

public sealed class SecurityHeadersTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApiResponse_EvenAnError_ForbidsSniffingFramingReferrerAndContent()
    {
        var response = await factory.CreateClient().GetAsync("/api/products", Ct);

        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        response.Headers.GetValues("Referrer-Policy").ShouldBe(["no-referrer"]);
        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["default-src 'none'; frame-ancestors 'none'"]);
    }

    [Fact]
    public async Task NonApiResponse_OnlyForbidsFraming()
    {
        var response = await factory.CreateClient().GetAsync("/health", Ct);

        response.Headers.GetValues("Content-Security-Policy").ShouldBe(["frame-ancestors 'none'"]);
    }

    [Fact]
    public async Task HttpsRequest_OutsideDevelopment_GetsHsts()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://kamada.test") });

        var response = await client.GetAsync("/health", Ct);

        response.Headers.GetValues("Strict-Transport-Security").Single().ShouldStartWith("max-age=");
    }
}
