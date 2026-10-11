using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.IntegrationTests.Ordering;

/// <summary>Orders repeated with the same <c>RequestId</c> (the phone app's outbox retrying after a lost response).</summary>
public sealed class OrderRequestIdTests(KamadaApiFactory factory)
{
    [Fact]
    public async Task PlaceOrder_RepeatedRequestId_ReturnsTheFirstOrderAndReservesStockOnce()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync(stock: 10);
        var (clientId, addressId) = await admin.CreateClientAsync();
        var request = new PlaceOrderRequest(clientId, addressId, [new OrderLineRequest(product.Id, 3)], RequestId: Guid.CreateVersion7());

        var first = await seller.PostJsonAsync("/api/orders", request);
        var repeated = await seller.PostJsonAsync("/api/orders", request);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        repeated.StatusCode.ShouldBe(HttpStatusCode.Created);
        var placed = await first.ReadAsAsync<OrderDto>();
        var replayed = await repeated.ReadAsAsync<OrderDto>();
        replayed.Id.ShouldBe(placed.Id);
        replayed.Number.ShouldBe(placed.Number);
        replayed.Items.ShouldHaveSingleItem().Quantity.ShouldBe(3);
        (await admin.GetStockAsync(product.Id)).ShouldBe(7);
    }

    /// <remarks>
    /// A loser of the race either finds the winner's order (201) or fails on the product's concurrency token before the
    /// winner committed (409 <c>Concurrency.Conflict</c>); either way only one order is placed and a later retry gets it.
    /// </remarks>
    [Fact]
    public async Task PlaceOrder_ConcurrentRetries_PlaceOneOrder()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync(stock: 10);
        var (clientId, addressId) = await admin.CreateClientAsync();
        var request = new PlaceOrderRequest(clientId, addressId, [new OrderLineRequest(product.Id, 2)], RequestId: Guid.CreateVersion7());

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => seller.PostJsonAsync("/api/orders", request)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created || r.StatusCode == HttpStatusCode.Conflict);
        var ids = await Task.WhenAll(responses.Where(r => r.StatusCode == HttpStatusCode.Created).Select(async r => (await r.ReadAsAsync<OrderDto>()).Id));
        var orderId = ids.Distinct().ShouldHaveSingleItem();
        (await admin.GetStockAsync(product.Id)).ShouldBe(8);
        (await (await seller.PostJsonAsync("/api/orders", request)).ReadAsAsync<OrderDto>()).Id.ShouldBe(orderId);
    }

    [Fact]
    public async Task PlaceOrder_RequestIdUsedByAnotherWorker_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        var (clientId, addressId) = await admin.CreateClientAsync();
        var request = new PlaceOrderRequest(clientId, addressId, [new OrderLineRequest(product.Id, 1)], RequestId: Guid.CreateVersion7());
        (await admin.PostJsonAsync("/api/orders", request)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await seller.PostJsonAsync("/api/orders", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Requests.IdReused");
    }
}
