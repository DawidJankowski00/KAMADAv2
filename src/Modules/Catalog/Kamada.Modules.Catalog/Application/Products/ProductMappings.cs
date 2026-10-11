using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Contracts.Catalog;

namespace Kamada.Modules.Catalog.Application.Products;

internal static class ProductMappings
{
    public static ProductDto ToDto(this Product product, IReadOnlyDictionary<Guid, string> categoryNames) => new(
        product.Id,
        product.Name,
        product.Description,
        product.Sku,
        product.Price,
        product.StockQuantity,
        product.IsActive,
        product.CreatedAt,
        [.. product.Categories
            .Where(c => categoryNames.ContainsKey(c.CategoryId))
            .Select(c => new CategoryRefDto(c.CategoryId, categoryNames[c.CategoryId]))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)]);

    public static ProductLookupDto ToLookupDto(this Product product, IReadOnlyDictionary<Guid, string> categoryNames) => new(
        product.Id,
        product.Sku,
        product.Name,
        product.Price,
        product.StockQuantity,
        [.. product.Categories
            .Where(c => categoryNames.ContainsKey(c.CategoryId))
            .Select(c => new CategoryRefDto(c.CategoryId, categoryNames[c.CategoryId]))
            .OrderBy(c => c.Name, StringComparer.CurrentCulture)]);
}
