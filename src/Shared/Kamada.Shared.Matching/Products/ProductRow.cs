namespace Kamada.Shared.Matching.Products;

/// <summary>Searchable projection of an active product.</summary>
public sealed record ProductRow(Guid Id, string Name, string Sku, decimal Price, int StockQuantity);
