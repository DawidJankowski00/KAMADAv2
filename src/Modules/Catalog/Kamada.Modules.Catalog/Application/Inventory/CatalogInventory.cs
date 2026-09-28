using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Inventory;

/// <summary>
/// Public stock API of the Catalog module. Concurrent reservations of the same product are protected by the
/// <c>xmin</c> concurrency token: the losing request gets a 409 and can be retried.
/// </summary>
internal sealed class CatalogInventory(ICatalogDbContext db) : ICatalogInventory
{
    public async Task<Result<IReadOnlyList<ReservedProduct>>> ReserveAsync(
        IReadOnlyList<StockLine> lines,
        CancellationToken cancellationToken)
    {
        var products = await LoadAsync(lines, cancellationToken);
        var missing = lines.Select(l => l.ProductId).Where(id => !products.ContainsKey(id)).Distinct().ToList();
        if (missing.Count > 0)
        {
            return Error.Validation("Products.NotFound", $"Unknown products: {string.Join(", ", missing)}.");
        }

        // Check every product first, so a failure never leaves half-modified entities in the change tracker.
        foreach (var (productId, quantity) in lines.GroupBy(l => l.ProductId).Select(g => (g.Key, g.Sum(l => l.Quantity))))
        {
            var check = products[productId].CanReserve(quantity);
            if (check.IsFailure)
            {
                return check.Error;
            }
        }

        var reserved = new List<ReservedProduct>(lines.Count);
        foreach (var line in lines)
        {
            var product = products[line.ProductId];
            product.ReserveStock(line.Quantity);
            reserved.Add(new ReservedProduct(product.Id, product.Name, product.Sku, product.Price, line.Quantity));
        }

        await db.SaveChangesAsync(cancellationToken);
        return reserved;
    }

    public async Task ReleaseAsync(IReadOnlyList<StockLine> lines, CancellationToken cancellationToken)
    {
        var products = await LoadAsync(lines, cancellationToken);
        foreach (var line in lines.Where(l => products.ContainsKey(l.ProductId)))
        {
            products[line.ProductId].ReleaseStock(line.Quantity);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<Dictionary<Guid, Product>> LoadAsync(IReadOnlyList<StockLine> lines, CancellationToken cancellationToken)
    {
        var ids = lines.Select(l => l.ProductId).Distinct().ToList();
        return await db.Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id, cancellationToken);
    }
}
