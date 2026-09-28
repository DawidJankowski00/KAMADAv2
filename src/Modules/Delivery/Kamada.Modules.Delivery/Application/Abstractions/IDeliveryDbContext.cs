using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Modules.Delivery.Domain.Runs;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Delivery.Application.Abstractions;

internal interface IDeliveryDbContext
{
    DbSet<RouteLocation> RouteLocations { get; }

    DbSet<DeliveryRun> DeliveryRuns { get; }

    DbSet<DeliveryRunOrder> DeliveryRunOrders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
