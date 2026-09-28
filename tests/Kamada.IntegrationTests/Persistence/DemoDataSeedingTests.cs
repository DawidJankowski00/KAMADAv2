using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Infrastructure.Seeding;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.IntegrationTests.Persistence;

/// <summary>The demo scenario from the README, seeded on a fresh database (matrix provider: Haversine).</summary>
public sealed class DemoDataSeedingTests(KamadaApiFactory factory)
{
    private const string Database = "kamada_demo_seed";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Startup_EmptyDatabase_SeedsDemoScenarioOnce()
    {
        await using (var host = factory.WithDemoData(Database))
        {
            var admin = await SignInAsync(host, "admin", WorkforceSeeder.AdminPassword);
            (await CountAsync<ProductDto>(admin, "/api/products")).ShouldBe(40);
            (await CountAsync<ClientSummaryDto>(admin, "/api/clients")).ShouldBe(26);

            var orders = await GetAsync<PagedResult<OrderSummaryDto>>(admin, "/api/orders?pageSize=100");
            orders.TotalCount.ShouldBe(36);
            orders.Items.Select(o => o.Status).Distinct().ShouldBeSubsetOf(Enum.GetValues<OrderStatus>().Except([OrderStatus.OutForDelivery]));
            orders.Items.Select(o => o.Status).ShouldContain(OrderStatus.New);
            orders.Items.Select(o => o.Status).ShouldContain(OrderStatus.ReadyForDelivery);
            orders.Items.Select(o => o.Status).ShouldContain(OrderStatus.Cancelled);
            orders.Items.Count(o => o.Status == OrderStatus.AssignedToRun).ShouldBe(8);
            orders.Items.Count(o => o.Status == OrderStatus.Delivered).ShouldBe(6);
            orders.Items.Count(o => o.Status == OrderStatus.DeliveryFailed).ShouldBe(1);
            orders.Items.ShouldAllBe(o => o.CreatedAt < DateTimeOffset.UtcNow);

            var courier = await SignInAsync(host, "kurier1", WorkforceSeeder.CourierPassword);
            var today = (await GetAsync<List<DeliveryRunSummaryDto>>(courier, "/api/courier/delivery-runs?scope=Current")).ShouldHaveSingleItem();
            today.Status.ShouldBe(DeliveryRunStatus.Planned);
            today.StopCount.ShouldBe(8);
            today.PlannedDurationSeconds.ShouldNotBeNull();

            var completed = (await GetAsync<List<DeliveryRunSummaryDto>>(courier, "/api/courier/delivery-runs?scope=Completed")).ShouldHaveSingleItem();
            completed.DeliveredCount.ShouldBe(6);
            completed.FailedCount.ShouldBe(1);

            var history = await GetAsync<DeliveryRunDto>(courier, $"/api/courier/delivery-runs/{completed.Id}");
            history.StartedAt.ShouldNotBeNull();
            history.CompletedAt.ShouldNotBeNull();
            history.CompletedAt.Value.ShouldBeGreaterThan(history.StartedAt.Value);
            history.Stops.Where(s => s.Status == DeliveryStopStatus.Delivered)
                .ShouldAllBe(s => s.DeliveredAt > history.StartedAt && s.DeliveredAt <= history.CompletedAt);
        }

        // A restart must not seed again.
        await using (var restarted = factory.WithDemoData(Database))
        {
            var admin = await SignInAsync(restarted, "admin", WorkforceSeeder.AdminPassword);
            (await CountAsync<OrderSummaryDto>(admin, "/api/orders")).ShouldBe(36);
            (await CountAsync<ProductDto>(admin, "/api/products")).ShouldBe(40);
        }
    }

    private static async Task<HttpClient> SignInAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host, string username, string password)
    {
        var client = host.CreateClient();
        var tokens = await client.LoginAsync(username, password);
        return client.WithBearer(tokens.AccessToken);
    }

    private static async Task<T> GetAsync<T>(HttpClient client, string url)
    {
        var response = await client.GetAsync(url, Ct);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<T>();
    }

    private static async Task<int> CountAsync<T>(HttpClient client, string url) =>
        (await GetAsync<PagedResult<T>>(client, url)).TotalCount;
}
