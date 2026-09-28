using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Catalog.Domain.Products;

/// <summary>
/// Sellable item. Products are never deleted (orders reference them) – they are deactivated instead.
/// Stock can only change through explicit methods, so the "never negative" invariant holds everywhere.
/// </summary>
internal sealed class Product : Entity, IAuditable
{
    public const decimal MaxPrice = 99_999_999.99m;

    private readonly List<ProductCategory> _categories = [];

    private Product(Guid id, string name, string? description, string sku, decimal price, int stockQuantity)
        : base(id)
    {
        Name = name;
        Description = description;
        Sku = sku;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
    }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public string Sku { get; private set; }

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<ProductCategory> Categories => _categories;

    public static string NormalizeSku(string sku) => sku.Trim().ToUpperInvariant();

    public static Result<Product> Create(
        string name,
        string? description,
        string sku,
        decimal price,
        int stockQuantity,
        IEnumerable<Guid> categoryIds)
    {
        var priceCheck = ValidatePrice(price);
        if (priceCheck.IsFailure)
        {
            return priceCheck.Error;
        }

        if (stockQuantity < 0)
        {
            return ProductErrors.NegativeStock;
        }

        var product = new Product(Guid.CreateVersion7(), name.Trim(), Clean(description), NormalizeSku(sku), price, stockQuantity);
        product.SetCategories(categoryIds);
        return product;
    }

    public Result Update(string name, string? description, string sku, decimal price)
    {
        var priceCheck = ValidatePrice(price);
        if (priceCheck.IsFailure)
        {
            return priceCheck;
        }

        Name = name.Trim();
        Description = Clean(description);
        Sku = NormalizeSku(sku);
        Price = price;
        return Result.Success();
    }

    public void SetCategories(IEnumerable<Guid> categoryIds)
    {
        var wanted = categoryIds.ToHashSet();
        _categories.RemoveAll(c => !wanted.Contains(c.CategoryId));
        foreach (var categoryId in wanted.Where(id => _categories.TrueForAll(c => c.CategoryId != id)))
        {
            _categories.Add(new ProductCategory(Id, categoryId));
        }
    }

    public Result SetStock(int quantity)
    {
        if (quantity < 0)
        {
            return ProductErrors.NegativeStock;
        }

        StockQuantity = quantity;
        return Result.Success();
    }

    /// <summary>Takes <paramref name="quantity"/> units out of stock for an order.</summary>
    public Result ReserveStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        if (!IsActive)
        {
            return ProductErrors.Inactive(Sku);
        }

        if (StockQuantity < quantity)
        {
            return ProductErrors.InsufficientStock(Sku, StockQuantity, quantity);
        }

        StockQuantity -= quantity;
        return Result.Success();
    }

    /// <summary>Returns units to stock (e.g. a cancelled order).</summary>
    public void ReleaseStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity must be positive.");
        }

        StockQuantity += quantity;
    }

    public void Deactivate() => IsActive = false;

    public void Activate() => IsActive = true;

    private static Result ValidatePrice(decimal price)
    {
        if (price <= 0 || price > MaxPrice)
        {
            return ProductErrors.InvalidPrice;
        }

        return decimal.Round(price, 2) == price ? Result.Success() : ProductErrors.InvalidPrice;
    }

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
