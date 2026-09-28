using Kamada.Modules.Catalog.Domain.Products;

namespace Kamada.Modules.Catalog.UnitTests;

public sealed class ProductTests
{
    private static Product CreateProduct(int stock = 10, params Guid[] categories) =>
        Product.Create("Kawa ziarnista 1 kg", "  Arabica  ", " kaw-001 ", 59.99m, stock, categories).Value;

    [Fact]
    public void Create_NormalizesSkuAndTrimsDescription()
    {
        var product = CreateProduct();

        product.Sku.ShouldBe("KAW-001");
        product.Description.ShouldBe("Arabica");
        product.IsActive.ShouldBeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10.999)]
    [InlineData(100_000_000)]
    public void Create_InvalidPrice_Fails(decimal price)
    {
        var result = Product.Create("Name", null, "SKU-1", price, 1, []);

        result.Error.ShouldBe(ProductErrors.InvalidPrice);
    }

    [Fact]
    public void Create_NegativeStock_Fails()
    {
        Product.Create("Name", null, "SKU-1", 1m, -1, []).Error.ShouldBe(ProductErrors.NegativeStock);
    }

    [Fact]
    public void ReserveStock_WithEnoughStock_DecrementsStock()
    {
        var product = CreateProduct(stock: 5);

        product.ReserveStock(3).IsSuccess.ShouldBeTrue();

        product.StockQuantity.ShouldBe(2);
    }

    [Fact]
    public void ReserveStock_MoreThanAvailable_FailsAndKeepsStock()
    {
        var product = CreateProduct(stock: 2);

        var result = product.ReserveStock(3);

        result.Error.Code.ShouldBe("Products.InsufficientStock");
        product.StockQuantity.ShouldBe(2);
    }

    [Fact]
    public void ReserveStock_InactiveProduct_Fails()
    {
        var product = CreateProduct(stock: 5);
        product.Deactivate();

        product.ReserveStock(1).Error.Code.ShouldBe("Products.Inactive");
    }

    [Fact]
    public void ReleaseStock_AddsUnitsBack()
    {
        var product = CreateProduct(stock: 1);

        product.ReleaseStock(4);

        product.StockQuantity.ShouldBe(5);
    }

    [Fact]
    public void SetCategories_SynchronizesAssignments()
    {
        var keep = Guid.NewGuid();
        var remove = Guid.NewGuid();
        var add = Guid.NewGuid();
        var product = CreateProduct(10, keep, remove);
        var keptLink = product.Categories.Single(c => c.CategoryId == keep);

        product.SetCategories([keep, add, add]);

        product.Categories.Select(c => c.CategoryId).ShouldBe([keep, add], ignoreOrder: true);
        product.Categories.ShouldContain(keptLink);
    }
}
