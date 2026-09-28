namespace Kamada.Shared.Contracts.Catalog;

public sealed record CategoryRefDto(Guid Id, string Name);

public sealed record ProductDto(
    Guid Id,
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<CategoryRefDto> Categories);

public sealed record CreateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    int StockQuantity,
    IReadOnlyList<Guid> CategoryIds);

public sealed record UpdateProductRequest(
    string Name,
    string? Description,
    string Sku,
    decimal Price,
    IReadOnlyList<Guid> CategoryIds);

/// <summary>Sets the absolute stock level (stock-taking / delivery from supplier).</summary>
public sealed record SetStockRequest(int StockQuantity);

public enum ProductSortBy
{
    Name,
    Price,
    Sku,
    Stock,
    CreatedAt,
}

/// <param name="Search">Matches name or SKU (case-insensitive, contains).</param>
/// <param name="CategoryId">Category filter; includes all subcategories.</param>
public sealed record ProductListQuery(
    string? Search = null,
    Guid? CategoryId = null,
    bool? IsActive = null,
    ProductSortBy SortBy = ProductSortBy.Name,
    bool Descending = false,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);
