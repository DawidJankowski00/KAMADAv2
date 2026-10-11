using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Ordering;

namespace Kamada.IntegrationTests.Ordering;

public sealed class PlacedByTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PlaceOrder_RecordsTheSignedInWorker()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, sellerWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();

        var order = await seller.PlaceOrderAsync((product.Id, 1));

        order.PlacedBy.ShouldBe(new PlacedByDto(sellerWorker.Id, $"Test {nameof(WorkerRole.Seller)}"));
        (await admin.GetOrderAsync(order.Id)).PlacedBy.ShouldBe(order.PlacedBy);
    }

    [Fact]
    public async Task Mine_AsSeller_ListsOnlyOwnOrders()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, sellerWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        var own = await seller.PlaceOrderAsync((product.Id, 1));
        await admin.PlaceOrderAsync((product.Id, 1));

        var page = await (await seller.GetAsync("/api/orders/mine", Ct)).ReadAsAsync<PagedResult<OrderSummaryDto>>();

        var summary = page.Items.ShouldHaveSingleItem();
        summary.Id.ShouldBe(own.Id);
        summary.PlacedBy!.WorkerId.ShouldBe(sellerWorker.Id);
    }

    [Fact]
    public async Task Mine_IgnoresAnotherWorkerPassedAsFilter()
    {
        var (admin, adminWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        await admin.PlaceOrderAsync((product.Id, 1));

        var page = await (await seller.GetAsync($"/api/orders/mine?placedBy={adminWorker.Id}", Ct)).ReadAsAsync<PagedResult<OrderSummaryDto>>();

        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Get_AsSellerOwnOrder_Returns200()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        var order = await seller.PlaceOrderAsync((product.Id, 1));

        var response = await seller.GetAsync($"/api/orders/{order.Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<OrderDto>()).Id.ShouldBe(order.Id);
    }

    [Fact]
    public async Task Get_AsSellerOrderOfSomeoneElse_Returns404()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var (otherSeller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        var byAdmin = await admin.PlaceOrderAsync((product.Id, 1));
        var byOtherSeller = await otherSeller.PlaceOrderAsync((product.Id, 1));

        (await seller.GetAsync($"/api/orders/{byAdmin.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await seller.GetAsync($"/api/orders/{byOtherSeller.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_AsAdminFilteredByPlacedBy_ReturnsThatWorkersOrders()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, sellerWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        var bySeller = await seller.PlaceOrderAsync((product.Id, 1));
        await admin.PlaceOrderAsync((product.Id, 1));

        var page = await (await admin.GetAsync($"/api/orders?placedBy={sellerWorker.Id}", Ct)).ReadAsAsync<PagedResult<OrderSummaryDto>>();

        page.Items.ShouldHaveSingleItem().Id.ShouldBe(bySeller.Id);
    }
}
