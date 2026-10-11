namespace Kamada.Shared.Matching.Products;

/// <summary>An active product similar to a phrase; <see cref="Score"/> is 0 … 1 (1 = exact SKU or name).</summary>
public sealed record ProductCandidate(Guid Id, string Name, string Sku, decimal Price, int StockQuantity, double Score);
