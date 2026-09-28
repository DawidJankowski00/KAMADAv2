using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Products;

internal sealed class ProductQueries(ICatalogDbContext db)
{
    public async Task<PagedResult<ProductDto>> ListAsync(ProductListQuery query, CancellationToken cancellationToken)
    {
        var products = db.Products.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            products = products.Where(p => EF.Functions.ILike(p.Name, pattern) || EF.Functions.ILike(p.Sku, pattern));
        }

        if (query.CategoryId is { } categoryId)
        {
            var tree = await LoadTreeAsync(cancellationToken);
            var categoryIds = tree.SelfAndDescendants(categoryId).ToList();
            products = products.Where(p => p.Categories.Any(c => categoryIds.Contains(c.CategoryId)));
        }

        if (query.IsActive is { } isActive)
        {
            products = products.Where(p => p.IsActive == isActive);
        }

        var total = await products.CountAsync(cancellationToken);
        var page = await Sort(products, query.SortBy, query.Descending)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(p => p.Categories)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var names = await CategoryNamesAsync(page.SelectMany(p => p.Categories.Select(c => c.CategoryId)), cancellationToken);
        return new PagedResult<ProductDto>([.. page.Select(p => p.ToDto(names))], query.Page, query.PageSize, total);
    }

    public async Task<Result<ProductDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Categories)
            .SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }

        var names = await CategoryNamesAsync(product.Categories.Select(c => c.CategoryId), cancellationToken);
        return product.ToDto(names);
    }

    private async Task<CategoryTree> LoadTreeAsync(CancellationToken cancellationToken)
    {
        var nodes = await db.Categories.AsNoTracking()
            .Select(c => new { c.Id, c.ParentId })
            .ToListAsync(cancellationToken);
        return new CategoryTree(nodes.Select(n => (n.Id, n.ParentId)));
    }

    private async Task<IReadOnlyDictionary<Guid, string>> CategoryNamesAsync(IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        var distinct = ids.Distinct().ToList();
        return await db.Categories.AsNoTracking()
            .Where(c => distinct.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => c.Name, cancellationToken);
    }

    private static IQueryable<Product> Sort(IQueryable<Product> products, ProductSortBy sortBy, bool descending) =>
        (sortBy, descending) switch
        {
            (ProductSortBy.Price, false) => products.OrderBy(p => p.Price).ThenBy(p => p.Id),
            (ProductSortBy.Price, true) => products.OrderByDescending(p => p.Price).ThenBy(p => p.Id),
            (ProductSortBy.Sku, false) => products.OrderBy(p => p.Sku),
            (ProductSortBy.Sku, true) => products.OrderByDescending(p => p.Sku),
            (ProductSortBy.Stock, false) => products.OrderBy(p => p.StockQuantity).ThenBy(p => p.Id),
            (ProductSortBy.Stock, true) => products.OrderByDescending(p => p.StockQuantity).ThenBy(p => p.Id),
            (ProductSortBy.CreatedAt, false) => products.OrderBy(p => p.CreatedAt).ThenBy(p => p.Id),
            (ProductSortBy.CreatedAt, true) => products.OrderByDescending(p => p.CreatedAt).ThenBy(p => p.Id),
            (_, false) => products.OrderBy(p => p.Name).ThenBy(p => p.Id),
            (_, true) => products.OrderByDescending(p => p.Name).ThenBy(p => p.Id),
        };
}
