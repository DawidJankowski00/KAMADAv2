namespace Kamada.Shared.Contracts.Catalog;

/// <param name="ProductCount">Products assigned directly to this category (not to subcategories).</param>
public sealed record CategoryNodeDto(
    Guid Id,
    string Name,
    Guid? ParentId,
    int ProductCount,
    IReadOnlyList<CategoryNodeDto> Children);

public sealed record CreateCategoryRequest(string Name, Guid? ParentId);

/// <summary>Renames and/or moves a category (null parent = root).</summary>
public sealed record UpdateCategoryRequest(string Name, Guid? ParentId);
