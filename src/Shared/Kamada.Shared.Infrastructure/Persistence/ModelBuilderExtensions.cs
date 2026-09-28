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
        foreach (var entityType in modelBuilder.Model.GetEntityTypes().Where(e => !e.IsOwned()))
        {
            modelBuilder.Entity(entityType.ClrType)
                .Property<uint>(ConcurrencyTokenProperty)
                .IsRowVersion();
        }

        return modelBuilder;
    }
}
