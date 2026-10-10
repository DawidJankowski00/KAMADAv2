using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Lookup;

/// <summary>
/// Public product search of the Catalog module. Matching runs in memory over a narrow projection of active
/// products – the catalogue of a small shop fits easily; a large one would pre-filter in SQL first.
/// </summary>
internal sealed class CatalogLookup(ICatalogDbContext db) : ICatalogLookup
{
    public async Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(phrase) || limit <= 0)
        {
            return [];
        }

        var products = await db.Products.AsNoTracking()
            .Where(p => p.IsActive)
            .Select(p => new ProductRow(p.Id, p.Name, p.Sku, p.Price, p.StockQuantity))
            .ToListAsync(cancellationToken);

        return ProductMatcher.Rank(phrase, products, limit);
    }
}
