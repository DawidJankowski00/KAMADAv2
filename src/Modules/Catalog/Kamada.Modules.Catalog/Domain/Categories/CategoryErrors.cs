using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Catalog.Domain.Categories;

internal static class CategoryErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Categories.NotFound", "Category was not found.");

    public static readonly Error ParentNotFound =
        Error.Validation("Categories.ParentNotFound", "Parent category does not exist.");

    public static readonly Error NameTaken =
        Error.Conflict("Categories.NameTaken", "A category with this name already exists at this level.");

    public static readonly Error Cycle =
        Error.Conflict("Categories.Cycle", "A category cannot be moved under itself or one of its subcategories.");

    public static readonly Error HasChildren =
        Error.Conflict("Categories.HasChildren", "Delete or move the subcategories first.");
}
