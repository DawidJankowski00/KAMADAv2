using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;

namespace Kamada.IntegrationTests.Workforce;

public sealed class LoginRateLimitTests(KamadaApiFactory factory)
{
    [Fact]
    public async Task Login_AfterPermitLimitExceeded_Returns429()
    {
        // Separate host with its own limiter state, so other tests are not throttled.
        await using var limited = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("RateLimiting:Login:PermitLimit", "3"));
        var client = limited.CreateClient();
        var request = new LoginRequest("someone", "Wrong1234!");

        for (var attempt = 0; attempt < 3; attempt++)
        {
            (await client.PostJsonAsync("/api/auth/login", request)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var blocked = await client.PostJsonAsync("/api/auth/login", request);

        blocked.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        blocked.Headers.RetryAfter.ShouldNotBeNull();
    }
}
