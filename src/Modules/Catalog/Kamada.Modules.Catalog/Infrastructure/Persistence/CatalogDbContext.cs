using Kamada.Modules.Catalog.Application.Abstractions;
using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Infrastructure.Persistence;

internal sealed class CatalogDbContext(DbContextOptions<CatalogDbContext> options)
    : DbContext(options), ICatalogDbContext
{
    public const string Schema = "catalog";

    public DbSet<Product> Products => Set<Product>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CatalogDbContext).Assembly,
            t => t.Namespace == typeof(CatalogDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
