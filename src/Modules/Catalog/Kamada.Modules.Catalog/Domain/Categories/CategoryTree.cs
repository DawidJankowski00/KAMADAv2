namespace Kamada.Modules.Catalog.Domain.Categories;

/// <summary>
/// In-memory view of the category hierarchy (the tree is small: tens to hundreds of nodes),
/// used for rules and queries that span several levels.
/// </summary>
internal sealed class CategoryTree
{
    private readonly Dictionary<Guid, Guid?> _parentById;
    private readonly ILookup<Guid?, Guid> _childrenByParent;

    public CategoryTree(IEnumerable<(Guid Id, Guid? ParentId)> nodes)
    {
        var list = nodes.ToList();
        _parentById = list.ToDictionary(n => n.Id, n => n.ParentId);
        _childrenByParent = list.ToLookup(n => n.ParentId, n => n.Id);
    }

    public bool Contains(Guid id) => _parentById.ContainsKey(id);

    public bool HasChildren(Guid id) => _childrenByParent[id].Any();

    /// <summary>The category itself plus all categories below it (breadth-first).</summary>
    public IReadOnlySet<Guid> SelfAndDescendants(Guid id)
    {
        var result = new HashSet<Guid>();
        if (!Contains(id))
        {
            return result;
        }

        var queue = new Queue<Guid>([id]);
        while (queue.TryDequeue(out var current))
        {
            if (!result.Add(current))
            {
                continue;
            }

            foreach (var child in _childrenByParent[current])
            {
                queue.Enqueue(child);
            }
        }

        return result;
    }

    /// <summary>
    /// Moving <paramref name="id"/> under <paramref name="newParentId"/> creates a cycle when the new parent is
    /// the category itself or one of its descendants.
    /// </summary>
    public bool WouldCreateCycle(Guid id, Guid? newParentId) =>
        newParentId is { } parent && SelfAndDescendants(id).Contains(parent);
}
