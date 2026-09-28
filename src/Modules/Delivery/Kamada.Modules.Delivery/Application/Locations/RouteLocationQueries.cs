using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Delivery.Application.Locations;

internal sealed class RouteLocationQueries(IDeliveryDbContext db)
{
    public async Task<IReadOnlyList<RouteLocationDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken)
    {
        var locations = await db.RouteLocations.AsNoTracking()
            .Where(l => includeInactive || l.IsActive)
            .OrderBy(l => l.Name)
            .ToListAsync(cancellationToken);
        return [.. locations.Select(l => l.ToDto())];
    }

    public async Task<Result<RouteLocationDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var location = await db.RouteLocations.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        return location is null ? DeliveryErrors.LocationNotFound : location.ToDto();
    }
}
