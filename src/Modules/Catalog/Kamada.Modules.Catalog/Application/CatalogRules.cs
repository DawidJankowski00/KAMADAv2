namespace Kamada.Modules.Catalog.Application;

/// <summary>Input limits shared by validators and EF column definitions.</summary>
internal static class CatalogRules
{
    public const int ProductNameMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int SkuMaxLength = 40;
    public const int CategoryNameMaxLength = 100;
    public const int MaxCategoriesPerProduct = 20;
    public const int MaxStockQuantity = 1_000_000;
}
