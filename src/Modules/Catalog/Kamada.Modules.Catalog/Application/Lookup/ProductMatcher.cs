using Kamada.Modules.Catalog.Contracts;
using Kamada.Shared.Kernel.Text;

namespace Kamada.Modules.Catalog.Application.Lookup;

/// <summary>Searchable projection of an active product.</summary>
internal sealed record ProductRow(Guid Id, string Name, string Sku, decimal Price, int StockQuantity);

/// <summary>Ranks products against a phrase: exact SKU first, then fuzzy similarity of the name.</summary>
internal static class ProductMatcher
{
    /// <summary>Products less similar than this are not worth showing as candidates.</summary>
    public const double MinScore = 0.6;

    public static IReadOnlyList<ProductCandidate> Rank(string phrase, IEnumerable<ProductRow> products, int limit)
    {
        var sku = TextSimilarity.Compact(phrase);
        return products
            .Select(p => (Product: p, Score: TextSimilarity.Compact(p.Sku) == sku ? 1 : NameScore(phrase, sku, p.Name)))
            .Where(x => x.Score >= MinScore)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Product.Name, StringComparer.Ordinal)
            .Take(limit)
            .Select(x => new ProductCandidate(
                x.Product.Id, x.Product.Name, x.Product.Sku, x.Product.Price, x.Product.StockQuantity, Math.Round(x.Score, 3)))
            .ToList();
    }

    /// <summary>Word by word, or the phrase written as one word when the name has that word ("power bank" → "Powerbank").</summary>
    private static double NameScore(string phrase, string compactPhrase, string name)
    {
        var byWords = TextSimilarity.PhraseScore(phrase, name);
        var nameHasJoinedWord = TextSimilarity.Tokens(name).Any(word => TextSimilarity.JaroWinkler(compactPhrase, word) >= 0.95);
        return nameHasJoinedWord ? Math.Max(byWords, TextSimilarity.PhraseScore(compactPhrase, name)) : byWords;
    }
}
