using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Catalog.Domain.Products;

/// <summary>
/// Many-to-many link between a product and a category (<c>product_categories</c>).
/// </summary>
internal sealed class ProductCategory : Entity
{
    public ProductCategory(Guid productId, Guid categoryId)
        : base(Guid.CreateVersion7())
    {
        ProductId = productId;
        CategoryId = categoryId;
    }

    public Guid ProductId { get; private set; }

    public Guid CategoryId { get; private set; }
}
