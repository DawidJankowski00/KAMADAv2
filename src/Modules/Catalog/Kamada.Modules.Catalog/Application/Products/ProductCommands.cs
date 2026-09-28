using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Products;

internal sealed class ProductCommands(ICatalogDbContext db, ProductQueries queries)
{
    public async Task<Result<ProductDto>> CreateAsync(CreateProductRequest request, CancellationToken cancellationToken)
    {
        var check = await CheckSkuAndCategoriesAsync(productId: null, request.Sku, request.CategoryIds, cancellationToken);
        if (check.IsFailure)
        {
            return check.Error;
        }

        var created = Product.Create(request.Name, request.Description, request.Sku, request.Price, request.StockQuantity, request.CategoryIds);
        if (created.IsFailure)
        {
            return created.Error;
        }

        db.Products.Add(created.Value);
        await db.SaveChangesAsync(cancellationToken);
        return await queries.GetAsync(created.Value.Id, cancellationToken);
    }

    public async Task<Result<ProductDto>> UpdateAsync(Guid id, UpdateProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.Include(p => p.Categories).SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }

        var check = await CheckSkuAndCategoriesAsync(id, request.Sku, request.CategoryIds, cancellationToken);
        if (check.IsFailure)
        {
            return check.Error;
        }

        var updated = product.Update(request.Name, request.Description, request.Sku, request.Price);
        if (updated.IsFailure)
        {
            return updated.Error;
        }

        product.SetCategories(request.CategoryIds);
        await db.SaveChangesAsync(cancellationToken);
        return await queries.GetAsync(id, cancellationToken);
    }

    public Task<Result> SetStockAsync(Guid id, SetStockRequest request, CancellationToken cancellationToken) =>
        ModifyAsync(id, product => product.SetStock(request.StockQuantity), cancellationToken);

    public Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, product =>
        {
            product.Deactivate();
            return Result.Success();
        }, cancellationToken);

    public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ModifyAsync(id, product =>
        {
            product.Activate();
            return Result.Success();
        }, cancellationToken);

    private async Task<Result> ModifyAsync(Guid id, Func<Product, Result> change, CancellationToken cancellationToken)
    {
        var product = await db.Products.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (product is null)
        {
            return ProductErrors.NotFound;
        }

        var result = change(product);
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    private async Task<Result> CheckSkuAndCategoriesAsync(
        Guid? productId,
        string sku,
        IReadOnlyList<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        var normalizedSku = Product.NormalizeSku(sku);
        if (await db.Products.AnyAsync(p => p.Sku == normalizedSku && p.Id != productId, cancellationToken))
        {
            return ProductErrors.SkuTaken;
        }

        var distinct = categoryIds.Distinct().ToList();
        var existing = await db.Categories.CountAsync(c => distinct.Contains(c.Id), cancellationToken);
        return existing == distinct.Count ? Result.Success() : ProductErrors.UnknownCategory;
    }
}
