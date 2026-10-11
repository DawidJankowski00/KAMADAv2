using Kamada.Shared.Matching.Products;

namespace Kamada.Modules.Catalog.Contracts;

/// <summary>Read-only product search for other modules (e.g. resolving products named in an order draft).</summary>
public interface ICatalogLookup
{
    /// <summary>
    /// Active products whose name or SKU is similar to <paramref name="phrase"/> (diacritics folded, fuzzy words),
    /// best first, at most <paramref name="limit"/>; empty when nothing is similar enough.
    /// </summary>
    Task<IReadOnlyList<ProductCandidate>> FindProductCandidatesAsync(string phrase, int limit, CancellationToken cancellationToken);
}
