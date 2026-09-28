using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;

namespace Kamada.IntegrationTests.Catalog;

public sealed class ProductTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static string UniqueSku() => $"T-{Guid.NewGuid():N}"[..14].ToUpperInvariant();

    private static CreateProductRequest NewProduct(params Guid[] categoryIds) =>
        new("Herbata zielona", "Liściasta, 100 g", UniqueSku(), 19.99m, 25, categoryIds);

    private static async Task<Guid> CreateCategoryAsync(HttpClient admin, string name, Guid? parentId = null)
    {
        var response = await admin.PostJsonAsync("/api/categories", new CreateCategoryRequest(name, parentId));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.ReadAsAsync<CategoryRefDto>()).Id;
    }

    [Fact]
    public async Task Create_ReturnsProductWithNormalizedSkuAndCategories()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var category = await CreateCategoryAsync(admin, $"Napoje {Guid.NewGuid():N}");
        var request = NewProduct(category) with { Sku = "abc-" + Guid.NewGuid().ToString("N")[..8] };

        var response = await admin.PostJsonAsync("/api/products", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var product = await response.ReadAsAsync<ProductDto>();
        product.Sku.ShouldBe(request.Sku.ToUpperInvariant());
        product.Price.ShouldBe(19.99m);
        product.Categories.Single().Id.ShouldBe(category);
    }

    [Fact]
    public async Task Create_DuplicateSku_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var request = NewProduct();
        (await admin.PostJsonAsync("/api/products", request)).EnsureSuccessStatusCode();

        var duplicate = await admin.PostJsonAsync("/api/products", request with { Sku = request.Sku.ToLowerInvariant() });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ReadProblemCodeAsync()).ShouldBe("Products.SkuTaken");
    }

    [Fact]
    public async Task Create_UnknownCategory_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync("/api/products", NewProduct(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Products.UnknownCategory");
    }

    [Fact]
    public async Task Create_PriceWithThreeDecimals_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync("/api/products", NewProduct() with { Price = 1.005m });

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_FilterByParentCategory_IncludesProductsFromSubcategories()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var root = await CreateCategoryAsync(admin, $"Spożywcze {Guid.NewGuid():N}");
        var child = await CreateCategoryAsync(admin, "Kawa", root);
        var inChild = await (await admin.PostJsonAsync("/api/products", NewProduct(child))).ReadAsAsync<ProductDto>();
        var inRoot = await (await admin.PostJsonAsync("/api/products", NewProduct(root))).ReadAsAsync<ProductDto>();
        (await admin.PostJsonAsync("/api/products", NewProduct())).EnsureSuccessStatusCode();

        var page = await (await admin.GetAsync($"/api/products?categoryId={root}", Ct)).ReadAsAsync<PagedResult<ProductDto>>();

        page.Items.Select(p => p.Id).ShouldBe([inChild.Id, inRoot.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task List_SearchBySkuAndPaging_Works()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var prefix = $"PG{Guid.NewGuid():N}"[..10].ToUpperInvariant();
        for (var i = 0; i < 3; i++)
        {
            (await admin.PostJsonAsync("/api/products", NewProduct() with { Sku = $"{prefix}-{i}" })).EnsureSuccessStatusCode();
        }

        var page = await (await admin.GetAsync($"/api/products?search={prefix}&pageSize=2&page=2&sortBy=Sku", Ct))
            .ReadAsAsync<PagedResult<ProductDto>>();

        page.TotalCount.ShouldBe(3);
        page.TotalPages.ShouldBe(2);
        page.Items.Single().Sku.ShouldBe($"{prefix}-2");
    }

    [Fact]
    public async Task Deactivate_ThenActivate_TogglesFlag()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await (await admin.PostJsonAsync("/api/products", NewProduct())).ReadAsAsync<ProductDto>();

        (await admin.PostAsync($"/api/products/{product.Id}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await admin.GetAsync($"/api/products/{product.Id}", Ct)).ReadAsAsync<ProductDto>()).IsActive.ShouldBeFalse();

        (await admin.PostAsync($"/api/products/{product.Id}/activate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await (await admin.GetAsync($"/api/products/{product.Id}", Ct)).ReadAsAsync<ProductDto>()).IsActive.ShouldBeTrue();
    }

    [Fact]
    public async Task SetStock_NegativeValue_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var product = await (await admin.PostJsonAsync("/api/products", NewProduct())).ReadAsAsync<ProductDto>();

        var response = await admin.PutJsonAsync($"/api/products/{product.Id}/stock", new SetStockRequest(-1));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Update_ChangesFieldsAndCategories()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var first = await CreateCategoryAsync(admin, $"A {Guid.NewGuid():N}");
        var second = await CreateCategoryAsync(admin, $"B {Guid.NewGuid():N}");
        var product = await (await admin.PostJsonAsync("/api/products", NewProduct(first))).ReadAsAsync<ProductDto>();

        var response = await admin.PutJsonAsync(
            $"/api/products/{product.Id}",
            new UpdateProductRequest("Nowa nazwa", null, product.Sku, 25m, [second]));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsAsync<ProductDto>();
        updated.Name.ShouldBe("Nowa nazwa");
        updated.Price.ShouldBe(25m);
        updated.Categories.Select(c => c.Id).ShouldBe([second]);
    }

    [Fact]
    public async Task Products_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        (await courier.GetAsync("/api/products", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
