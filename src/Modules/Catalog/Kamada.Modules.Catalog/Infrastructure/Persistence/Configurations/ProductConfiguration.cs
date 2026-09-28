using Kamada.Modules.Catalog.Application;
using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Modules.Catalog.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", t =>
        {
            t.HasCheckConstraint("ck_products_price_positive", "price > 0");
            t.HasCheckConstraint("ck_products_stock_non_negative", "stock_quantity >= 0");
        });
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.Name).HasMaxLength(CatalogRules.ProductNameMaxLength).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(CatalogRules.DescriptionMaxLength);
        builder.Property(p => p.Sku).HasMaxLength(CatalogRules.SkuMaxLength).IsRequired();
        builder.HasIndex(p => p.Sku).IsUnique();
        builder.Property(p => p.Price).HasPrecision(10, 2);
        builder.HasIndex(p => p.Name);

        builder.HasMany(p => p.Categories)
            .WithOne()
            .HasForeignKey(pc => pc.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Categories).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class ProductCategoryConfiguration : IEntityTypeConfiguration<ProductCategory>
{
    public void Configure(EntityTypeBuilder<ProductCategory> builder)
    {
        builder.ToTable("product_categories");
        builder.HasKey(pc => pc.Id);
        builder.Property(pc => pc.Id).ValueGeneratedNever();
        builder.HasIndex(pc => new { pc.ProductId, pc.CategoryId }).IsUnique();

        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(pc => pc.CategoryId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(pc => pc.DomainEvents);
    }
}
