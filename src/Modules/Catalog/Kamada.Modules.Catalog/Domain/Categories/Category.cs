using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Catalog.Domain.Categories;

/// <summary>
/// Node of the category tree. Tree-wide rules (no cycles, unique names among siblings) are checked with
/// <see cref="CategoryTree"/> because they need the whole tree, not just this node.
/// </summary>
internal sealed class Category : Entity, IAuditable
{
    private Category(Guid id, string name, Guid? parentId)
        : base(id)
    {
        Name = name;
        ParentId = parentId;
    }

    public string Name { get; private set; }

    public Guid? ParentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static Category Create(string name, Guid? parentId) => new(Guid.CreateVersion7(), name.Trim(), parentId);

    public void Rename(string name) => Name = name.Trim();

    public void MoveTo(Guid? parentId) => ParentId = parentId;
}
