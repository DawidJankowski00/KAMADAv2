using Microsoft.EntityFrameworkCore;

namespace Kamada.Shared.Infrastructure.Persistence;

public static class ModelBuilderExtensions
{
    public const string ConcurrencyTokenProperty = "Version";

    /// <summary>
    /// Adds optimistic concurrency to every (non-owned) entity using PostgreSQL's system column <c>xmin</c>,
    /// mapped as a shadow property so the domain model stays persistence-ignorant.
    /// </summary>
    public static ModelBuilder UseXminConcurrencyTokens(this ModelBuilder modelBuilder)
    {
        // Only real entities: types discovered by convention but later configured as complex properties
        // (value objects) have no key and must not be promoted to entities here.
        var entityTypes = modelBuilder.Model.GetEntityTypes()
            .Where(e => !e.IsOwned() && e.FindPrimaryKey() is not null)
            .ToList();

        foreach (var entityType in entityTypes)
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property<uint>(ConcurrencyTokenProperty)
                .IsRowVersion();
        }

        return modelBuilder;
    }
}
