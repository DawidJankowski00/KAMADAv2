using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.IntegrationTests.Ordering;

public sealed class OrderTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PlaceOrder_DecreasesStockAndComputesTotalOnServer()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var coffee = await admin.CreateProductAsync(stock: 10, price: 39.99m);
        var tea = await admin.CreateProductAsync(stock: 5, price: 12.50m);

        var order = await admin.PlaceOrderAsync((coffee.Id, 3), (tea.Id, 2));

        order.Status.ShouldBe(OrderStatus.New);
        order.TotalAmount.ShouldBe(3 * 39.99m + 2 * 12.50m);
        order.Number.ShouldBeGreaterThan(0);
        order.Items.Single(i => i.ProductId == coffee.Id).UnitPrice.ShouldBe(39.99m);
        (await admin.GetStockAsync(coffee.Id)).ShouldBe(7);
        (await admin.GetStockAsync(tea.Id)).ShouldBe(3);
    }

    [Fact]
    public async Task PlaceOrder_SameProductTwice_IsMergedIntoOneLine()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(stock: 10);

        var order = await admin.PlaceOrderAsync((product.Id, 2), (product.Id, 3));

        order.Items.Single().Quantity.ShouldBe(5);
        (await admin.GetStockAsync(product.Id)).ShouldBe(5);
    }

    [Fact]
    public async Task PlaceOrder_MoreThanInStock_Returns409AndChangesNothing()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var plenty = await admin.CreateProductAsync(stock: 100);
        var scarce = await admin.CreateProductAsync(stock: 2);
        var (clientId, addressId) = await admin.CreateClientAsync();

        var response = await admin.PlaceOrderRawAsync(clientId, addressId, (plenty.Id, 5), (scarce.Id, 3));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Products.InsufficientStock");
        (await admin.GetStockAsync(plenty.Id)).ShouldBe(100);
        (await admin.GetStockAsync(scarce.Id)).ShouldBe(2);
        var orders = await (await admin.GetAsync($"/api/orders?clientId={clientId}", Ct)).ReadAsAsync<PagedResult<OrderSummaryDto>>();
        orders.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task PlaceOrder_InactiveProduct_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync();
        await admin.PostAsync($"/api/products/{product.Id}/deactivate", null, Ct);
        var (clientId, addressId) = await admin.CreateClientAsync();

        var response = await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1));

        (await response.ReadProblemCodeAsync()).ShouldBe("Products.Inactive");
    }

    [Fact]
    public async Task PlaceOrder_BillingAddress_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync();
        var (clientId, addressId) = await admin.CreateClientAsync(AddressType.Billing);

        var response = await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Clients.NotShippingAddress");
    }

    [Fact]
    public async Task PlaceOrder_SnapshotsShippingAddress()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync();

        var order = await admin.PlaceOrderAsync((product.Id, 1));

        order.ShippingAddress.Street.ShouldBe("Dworcowa");
        order.ShippingAddress.RecipientName.ShouldBe("Adam Klient");
        order.ShippingAddress.Latitude.ShouldNotBeNull();
    }

    [Fact]
    public async Task Cancel_ReturnsItemsToStock()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(stock: 4);
        var order = await admin.PlaceOrderAsync((product.Id, 3));

        (await admin.PostAsync($"/api/orders/{order.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await admin.GetStockAsync(product.Id)).ShouldBe(4);
        (await admin.GetOrderAsync(order.Id)).Status.ShouldBe(OrderStatus.Cancelled);
        (await admin.PostAsync($"/api/orders/{order.Id}/cancel", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.GetStockAsync(product.Id)).ShouldBe(4);
    }

    [Fact]
    public async Task ReadyForDelivery_UnpaidOrder_Returns409_PaidOrder_Returns204()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync();
        var unpaid = await admin.PlaceOrderAsync((product.Id, 1));
        var paid = await admin.CreatePaidOrderAsync();

        var rejected = await admin.PostAsync($"/api/orders/{unpaid.Id}/ready-for-delivery", null, Ct);
        var accepted = await admin.PostAsync($"/api/orders/{paid.Id}/ready-for-delivery", null, Ct);

        rejected.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await rejected.ReadProblemCodeAsync()).ShouldBe("Orders.InvalidTransition");
        accepted.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await admin.GetOrderAsync(paid.Id)).Status.ShouldBe(OrderStatus.ReadyForDelivery);
    }

    [Fact]
    public async Task List_FiltersByStatusAndClient()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(stock: 10);
        var (clientId, addressId) = await admin.CreateClientAsync();
        var first = await (await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1))).ReadAsAsync<OrderDto>();
        await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1));
        await admin.PostAsync($"/api/orders/{first.Id}/cancel", null, Ct);

        var cancelled = await (await admin.GetAsync($"/api/orders?clientId={clientId}&status=Cancelled", Ct))
            .ReadAsAsync<PagedResult<OrderSummaryDto>>();
        var byNumber = await (await admin.GetAsync($"/api/orders?search=%23{first.Number}", Ct))
            .ReadAsAsync<PagedResult<OrderSummaryDto>>();

        cancelled.Items.Single().Id.ShouldBe(first.Id);
        byNumber.Items.Single().Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task StatusCounts_ContainsEveryStatus()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var counts = await (await admin.GetAsync("/api/orders/status-counts", Ct)).ReadAsAsync<List<OrderStatusCountDto>>();

        counts.Select(c => c.Status).ShouldBe(Enum.GetValues<OrderStatus>(), ignoreOrder: true);
    }
}
