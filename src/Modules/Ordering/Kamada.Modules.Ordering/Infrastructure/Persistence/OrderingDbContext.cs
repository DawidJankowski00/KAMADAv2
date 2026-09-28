using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Infrastructure.Persistence;

internal sealed class OrderingDbContext(DbContextOptions<OrderingDbContext> options)
    : DbContext(options), IOrderingDbContext
{
    public const string Schema = "ordering";

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(OrderingDbContext).Assembly,
            t => t.Namespace == typeof(OrderingDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
