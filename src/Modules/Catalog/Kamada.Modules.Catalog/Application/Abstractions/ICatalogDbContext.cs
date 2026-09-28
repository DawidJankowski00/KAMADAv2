using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Application.Abstractions;

internal interface ICatalogDbContext
{
    DbSet<Product> Products { get; }

    DbSet<Category> Categories { get; }

    DbSet<ProductCategory> ProductCategories { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
