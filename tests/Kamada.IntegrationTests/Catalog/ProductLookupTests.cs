using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Catalog;

namespace Kamada.IntegrationTests.Catalog;

public sealed class ProductLookupTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lookup_AsSeller_ListsActiveProductsWithStock()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var active = await admin.CreateProductAsync(stock: 7, price: 12.30m);
        var inactive = await admin.CreateProductAsync();
        (await admin.PostAsync($"/api/products/{inactive.Id}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await seller.GetAsync("/api/products/lookup", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var products = await response.ReadAsAsync<List<ProductLookupDto>>();
        var found = products.Single(p => p.Id == active.Id);
        (found.Sku, found.Name, found.Price, found.StockQuantity).ShouldBe((active.Sku, active.Name, 12.30m, 7));
        products.ShouldNotContain(p => p.Id == inactive.Id);
    }

    [Fact]
    public async Task Lookup_IfNoneMatchCurrentTag_Returns304WithoutBody()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);

        var notModified = await seller.AnswersNotModifiedForCurrentTagAsync("/api/products/lookup");

        notModified.ShouldBeTrue();
    }

    [Fact]
    public async Task Lookup_AfterStockChange_ReturnsDataWithNewTag()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await admin.CreateProductAsync(stock: 5);
        var etag = (await admin.GetAsync("/api/products/lookup", Ct)).Headers.ETag!;
        (await admin.PutJsonAsync($"/api/products/{product.Id}/stock", new SetStockRequest(4))).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await admin.GetIfNoneMatchAsync("/api/products/lookup", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBe(etag);
        (await response.ReadAsAsync<List<ProductLookupDto>>()).Single(p => p.Id == product.Id).StockQuantity.ShouldBe(4);
    }
}
