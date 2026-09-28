using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Delivery.Infrastructure.Persistence;

internal sealed class DeliveryDbContext(DbContextOptions<DeliveryDbContext> options)
    : DbContext(options), IDeliveryDbContext
{
    public const string Schema = "delivery";

    public DbSet<RouteLocation> RouteLocations => Set<RouteLocation>();

    public DbSet<DeliveryRun> DeliveryRuns => Set<DeliveryRun>();

    public DbSet<DeliveryRunOrder> DeliveryRunOrders => Set<DeliveryRunOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(DeliveryDbContext).Assembly,
            t => t.Namespace == typeof(DeliveryDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
