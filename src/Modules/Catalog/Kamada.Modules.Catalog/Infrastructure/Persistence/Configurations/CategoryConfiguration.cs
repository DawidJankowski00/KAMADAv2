using Kamada.Modules.Catalog.Application;
using Kamada.Modules.Catalog.Domain.Categories;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Catalog.Infrastructure.Persistence.Configurations;

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.Name).HasMaxLength(CatalogRules.CategoryNameMaxLength).IsRequired();

        // Restrict: a category with children cannot be deleted (checked by the application first).
        builder.HasOne<Category>()
            .WithMany()
            .HasForeignKey(c => c.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Sibling names are unique; NULLS NOT DISTINCT makes this hold for root categories too (PostgreSQL 15+).
        builder.HasIndex(c => new { c.ParentId, c.Name }).IsUnique().AreNullsDistinct(false);

        builder.Ignore(c => c.DomainEvents);
    }
}
