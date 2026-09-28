using FluentValidation;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;

namespace Kamada.Modules.Catalog.Application.Products;

internal static class ProductRuleExtensions
{
    public static IRuleBuilderOptions<T, string> ValidSku<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty()
            .MaximumLength(CatalogRules.SkuMaxLength)
            .Matches("^[A-Za-z0-9-]+$")
            .WithMessage("SKU may contain only letters, digits and '-'.");

    public static IRuleBuilderOptions<T, decimal> ValidPrice<T>(this IRuleBuilder<T, decimal> rule) =>
        rule.GreaterThan(0)
            .LessThanOrEqualTo(Product.MaxPrice)
            .PrecisionScale(10, 2, ignoreTrailingZeros: true);

    public static IRuleBuilderOptions<T, IReadOnlyList<Guid>> ValidCategoryIds<T>(this IRuleBuilder<T, IReadOnlyList<Guid>> rule) =>
        rule.NotNull()
            .Must(ids => ids.Count <= CatalogRules.MaxCategoriesPerProduct)
            .WithMessage($"A product can belong to at most {CatalogRules.MaxCategoriesPerProduct} categories.");
}

internal sealed class CreateProductRequestValidator : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(CatalogRules.ProductNameMaxLength);
        RuleFor(r => r.Description).MaximumLength(CatalogRules.DescriptionMaxLength);
        RuleFor(r => r.Sku).ValidSku();
        RuleFor(r => r.Price).ValidPrice();
        RuleFor(r => r.StockQuantity).InclusiveBetween(0, CatalogRules.MaxStockQuantity);
        RuleFor(r => r.CategoryIds).ValidCategoryIds();
    }
}

internal sealed class UpdateProductRequestValidator : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(r => r.Name).NotEmpty().MaximumLength(CatalogRules.ProductNameMaxLength);
        RuleFor(r => r.Description).MaximumLength(CatalogRules.DescriptionMaxLength);
        RuleFor(r => r.Sku).ValidSku();
        RuleFor(r => r.Price).ValidPrice();
        RuleFor(r => r.CategoryIds).ValidCategoryIds();
    }
}

internal sealed class SetStockRequestValidator : AbstractValidator<SetStockRequest>
{
    public SetStockRequestValidator() =>
        RuleFor(r => r.StockQuantity).InclusiveBetween(0, CatalogRules.MaxStockQuantity);
}

internal sealed class ProductListQueryValidator : AbstractValidator<ProductListQuery>
{
    public ProductListQueryValidator()
    {
        RuleFor(q => q.Search).MaximumLength(100);
        RuleFor(q => q.SortBy).IsInEnum();
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, Paging.MaxPageSize);
    }
}
