using System.Net;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Contracts.Payments;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>Arranges route locations, ready orders and delivery runs through the admin API.</summary>
internal static class DeliveryTestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    public static async Task<RouteLocationDto> CreateRouteLocationAsync(this HttpClient admin, string street = "Fordońska", string buildingNumber = "1")
    {
        var response = await admin.PostJsonAsync(
            "/api/route-locations",
            new RouteLocationRequest($"Magazyn {Guid.NewGuid():N}"[..30], street, buildingNumber, "Bydgoszcz", "85-719"));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<RouteLocationDto>();
    }

    /// <summary>Paid, packed order (status ReadyForDelivery) for a fresh client.</summary>
    public static async Task<OrderDto> CreateReadyOrderAsync(this HttpClient admin, string buildingNumber = "10", string street = "Dworcowa")
    {
        var product = await admin.CreateProductAsync();
        var (clientId, addressId) = await admin.CreateClientAsync(buildingNumber: buildingNumber, street: street);
        var placed = await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1));
        placed.StatusCode.ShouldBe(HttpStatusCode.Created);
        var order = await placed.ReadAsAsync<OrderDto>();

        (await admin.PostJsonAsync($"/api/orders/{order.Id}/payments", new RegisterPaymentRequest(PaymentProvider.Cash)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        (await admin.PostAsync($"/api/orders/{order.Id}/ready-for-delivery", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        return await admin.GetOrderAsync(order.Id);
    }

    public static async Task<IReadOnlyList<OrderDto>> CreateReadyOrdersAsync(this HttpClient admin, int count)
    {
        var orders = new List<OrderDto>();
        for (var i = 0; i < count; i++)
        {
            orders.Add(await admin.CreateReadyOrderAsync(buildingNumber: $"{(i + 1) * 7}"));
        }

        return orders;
    }

    public static Task<HttpResponseMessage> CreateRunRawAsync(
        this HttpClient admin,
        Guid courierId,
        Guid locationId,
        IReadOnlyList<Guid>? orderIds = null,
        DateOnly? plannedDate = null) =>
        admin.PostJsonAsync("/api/delivery-runs", new CreateDeliveryRunRequest(
            "Trasa testowa",
            plannedDate ?? Today,
            courierId,
            "CB 12345",
            locationId,
            locationId,
            orderIds));

    public static async Task<DeliveryRunDto> CreateRunAsync(
        this HttpClient admin,
        Guid courierId,
        Guid locationId,
        IReadOnlyList<Guid>? orderIds = null,
        DateOnly? plannedDate = null)
    {
        var response = await admin.CreateRunRawAsync(courierId, locationId, orderIds, plannedDate);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<DeliveryRunDto>();
    }

    /// <summary>Run with the given number of ready orders, optimized and approved (status Planned).</summary>
    public static async Task<DeliveryRunDto> CreatePlannedRunAsync(this HttpClient admin, Guid courierId, int stops = 2, DateOnly? plannedDate = null)
    {
        var location = await admin.CreateRouteLocationAsync();
        var orders = await admin.CreateReadyOrdersAsync(stops);
        var run = await admin.CreateRunAsync(courierId, location.Id, [.. orders.Select(o => o.Id)], plannedDate);

        (await admin.PostAsync($"/api/delivery-runs/{run.Id}/apply-optimization", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var approved = await admin.PostAsync($"/api/delivery-runs/{run.Id}/approve", null, Ct);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await approved.ReadAsAsync<DeliveryRunDto>();
    }

    public static async Task<DeliveryRunDto> GetRunAsync(this HttpClient admin, Guid runId) =>
        await (await admin.GetAsync($"/api/delivery-runs/{runId}", Ct)).ReadAsAsync<DeliveryRunDto>();
}
