using FluentValidation;
using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Categories;

/// <summary>
/// Category tree use cases. The whole tree is loaded for writes: it is small, and rules such as
/// "no cycles" and "unique name among siblings" need it anyway.
/// </summary>
internal sealed class CategoryService(ICatalogDbContext db)
{
    public async Task<IReadOnlyList<CategoryNodeDto>> GetTreeAsync(CancellationToken cancellationToken)
    {
        var categories = await db.Categories.AsNoTracking().ToListAsync(cancellationToken);
        var productCounts = await db.ProductCategories.AsNoTracking()
            .GroupBy(pc => pc.CategoryId)
            .Select(g => new { CategoryId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CategoryId, x => x.Count, cancellationToken);

        var childrenByParent = categories.ToLookup(c => c.ParentId);

        IReadOnlyList<CategoryNodeDto> Build(Guid? parentId) =>
        [
            .. childrenByParent[parentId]
                .OrderBy(c => c.Name, StringComparer.CurrentCulture)
                .Select(c => new CategoryNodeDto(c.Id, c.Name, c.ParentId, productCounts.GetValueOrDefault(c.Id), Build(c.Id))),
        ];

        return Build(null);
    }

    public async Task<Result<CategoryRefDto>> CreateAsync(CreateCategoryRequest request, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.ToListAsync(cancellationToken);
        var check = CheckPlacement(categories, categoryId: null, request.Name, request.ParentId);
        if (check.IsFailure)
        {
            return check.Error;
        }

        var category = Category.Create(request.Name, request.ParentId);
        db.Categories.Add(category);
        await db.SaveChangesAsync(cancellationToken);
        return new CategoryRefDto(category.Id, category.Name);
    }

    public async Task<Result> UpdateAsync(Guid id, UpdateCategoryRequest request, CancellationToken cancellationToken)
    {
        var categories = await db.Categories.ToListAsync(cancellationToken);
        var category = categories.SingleOrDefault(c => c.Id == id);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        var check = CheckPlacement(categories, id, request.Name, request.ParentId);
        if (check.IsFailure)
        {
            return check;
        }

        category.Rename(request.Name);
        category.MoveTo(request.ParentId);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Deletes a leaf category; product assignments are removed, products stay.</summary>
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken)
    {
        var category = await db.Categories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return CategoryErrors.NotFound;
        }

        if (await db.Categories.AnyAsync(c => c.ParentId == id, cancellationToken))
        {
            return CategoryErrors.HasChildren;
        }

        db.Categories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static Result CheckPlacement(IReadOnlyList<Category> categories, Guid? categoryId, string name, Guid? parentId)
    {
        var tree = new CategoryTree(categories.Select(c => (c.Id, c.ParentId)));
        if (parentId is { } parent && !tree.Contains(parent))
        {
            return CategoryErrors.ParentNotFound;
        }

        if (categoryId is { } id && tree.WouldCreateCycle(id, parentId))
        {
            return CategoryErrors.Cycle;
        }

        var trimmed = name.Trim();
        var nameTaken = categories.Any(c =>
            c.ParentId == parentId
            && c.Id != categoryId
            && string.Equals(c.Name, trimmed, StringComparison.CurrentCultureIgnoreCase));

        return nameTaken ? CategoryErrors.NameTaken : Result.Success();
    }
}

internal sealed class CreateCategoryRequestValidator : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator() => RuleFor(r => r.Name).NotEmpty().MaximumLength(CatalogRules.CategoryNameMaxLength);
}

internal sealed class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator() => RuleFor(r => r.Name).NotEmpty().MaximumLength(CatalogRules.CategoryNameMaxLength);
}
