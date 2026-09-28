using System.Net;
using Kamada.IntegrationTests.Infrastructure;

namespace Kamada.IntegrationTests;

public sealed class HealthTests(KamadaApiFactory factory)
{
    [Fact]
    public async Task Health_WhenDatabaseIsReachable_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken)).ShouldBe("Healthy");
    }
}
