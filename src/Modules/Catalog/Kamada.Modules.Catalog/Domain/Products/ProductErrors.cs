using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Catalog.Domain.Products;

internal static class ProductErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Products.NotFound", "Product was not found.");

    public static readonly Error SkuTaken =
        Error.Conflict("Products.SkuTaken", "A product with this SKU already exists.");

    public static readonly Error InvalidPrice =
        Error.Validation("Products.InvalidPrice", $"Price must be greater than 0, at most {Product.MaxPrice} and have at most 2 decimal places.");

    public static readonly Error NegativeStock =
        Error.Validation("Products.NegativeStock", "Stock quantity cannot be negative.");

    public static readonly Error UnknownCategory =
        Error.Validation("Products.UnknownCategory", "One or more categories do not exist.");

    public static Error Inactive(string sku) =>
        Error.Conflict("Products.Inactive", $"Product {sku} is inactive and cannot be ordered.");

    public static Error InsufficientStock(string sku, int available, int requested) =>
        Error.Conflict("Products.InsufficientStock", $"Not enough stock for {sku}: requested {requested}, available {available}.");
}
