namespace Kamada.Modules.Catalog.Contracts;

/// <summary>An active product similar to a phrase; <see cref="Score"/> is 0 … 1 (1 = exact SKU or name).</summary>
public sealed record ProductCandidate(Guid Id, string Name, string Sku, decimal Price, int StockQuantity, double Score);

/// <summary>Read-only product search for other modules (e.g. resolving products named in an order draft).</summary>
public interface ICatalogLookup
{
    /// <summary>
    /// Active products whose name or SKU is similar to <paramref name="phrase"/> (diacritics folded, fuzzy words),
    /// best first, at most <paramref name="limit"/>; empty when nothing is similar enough.
    /// </summary>
    Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken);
}
