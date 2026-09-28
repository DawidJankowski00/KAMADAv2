using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Catalog;

namespace Kamada.IntegrationTests.Catalog;

public sealed class CategoryTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static async Task<Guid> CreateAsync(HttpClient admin, string name, Guid? parentId = null)
    {
        var response = await admin.PostJsonAsync("/api/categories", new CreateCategoryRequest(name, parentId));
        response.EnsureSuccessStatusCode();
        return (await response.ReadAsAsync<CategoryRefDto>()).Id;
    }

    [Fact]
    public async Task GetTree_ReturnsNestedCategories()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var rootName = $"Root {Guid.NewGuid():N}";
        var root = await CreateAsync(admin, rootName);
        var child = await CreateAsync(admin, "Child", root);
        await CreateAsync(admin, "Grandchild", child);

        var tree = await (await admin.GetAsync("/api/categories", Ct)).ReadAsAsync<List<CategoryNodeDto>>();

        var node = tree.Single(n => n.Id == root);
        node.Children.Single().Id.ShouldBe(child);
        node.Children.Single().Children.Single().Name.ShouldBe("Grandchild");
    }

    [Fact]
    public async Task Create_DuplicateSiblingName_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var root = await CreateAsync(admin, $"Root {Guid.NewGuid():N}");
        await CreateAsync(admin, "Owoce", root);

        var duplicate = await admin.PostJsonAsync("/api/categories", new CreateCategoryRequest("OWOCE", root));

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ReadProblemCodeAsync()).ShouldBe("Categories.NameTaken");
    }

    [Fact]
    public async Task Move_UnderOwnDescendant_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var root = await CreateAsync(admin, $"Root {Guid.NewGuid():N}");
        var child = await CreateAsync(admin, "Child", root);

        var response = await admin.PutJsonAsync($"/api/categories/{root}", new UpdateCategoryRequest("Root", child));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Categories.Cycle");
    }

    [Fact]
    public async Task Delete_CategoryWithChildren_Returns409_LeafCanBeDeleted()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var root = await CreateAsync(admin, $"Root {Guid.NewGuid():N}");
        var leaf = await CreateAsync(admin, "Leaf", root);
        var product = await (await admin.PostJsonAsync(
                "/api/products",
                new CreateProductRequest("P", null, $"D-{Guid.NewGuid():N}"[..12], 1m, 1, [leaf])))
            .ReadAsAsync<ProductDto>();

        (await admin.DeleteAsync($"/api/categories/{root}", Ct)).StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await admin.DeleteAsync($"/api/categories/{leaf}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reloaded = await (await admin.GetAsync($"/api/products/{product.Id}", Ct)).ReadAsAsync<ProductDto>();
        reloaded.Categories.ShouldBeEmpty();
    }
}
