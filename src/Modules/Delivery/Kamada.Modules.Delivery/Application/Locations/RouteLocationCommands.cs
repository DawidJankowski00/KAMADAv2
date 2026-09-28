using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Delivery.Application.Locations;

/// <summary>
/// Route locations are geocoded on save like client addresses: a failed lookup leaves the location without
/// coordinates (pin it manually), it never fails the save.
/// </summary>
internal sealed class RouteLocationCommands(IDeliveryDbContext db, IGeocodingService geocoding)
{
    public static readonly Error NameTaken =
        Error.Conflict("Delivery.LocationNameTaken", "A route location with this name already exists.");

    public async Task<Result<RouteLocationDto>> CreateAsync(RouteLocationRequest request, CancellationToken cancellationToken)
    {
        var details = ToDetails(request);
        if (await IsNameTakenAsync(details.Name, exceptId: null, cancellationToken))
        {
            return NameTaken;
        }

        var location = RouteLocation.Create(details);
        await TryGeocodeAsync(location, cancellationToken);
        db.RouteLocations.Add(location);
        await db.SaveChangesAsync(cancellationToken);
        return location.ToDto();
    }

    public async Task<Result<RouteLocationDto>> UpdateAsync(Guid id, RouteLocationRequest request, CancellationToken cancellationToken)
    {
        var location = await db.RouteLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (location is null)
        {
            return DeliveryErrors.LocationNotFound;
        }

        var details = ToDetails(request);
        if (await IsNameTakenAsync(details.Name, exceptId: id, cancellationToken))
        {
            return NameTaken;
        }

        if (location.Update(details))
        {
            await TryGeocodeAsync(location, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);
        return location.ToDto();
    }

    public async Task<Result<RouteLocationDto>> SetPinAsync(Guid id, SetRouteLocationPinRequest request, CancellationToken cancellationToken)
    {
        var location = await db.RouteLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (location is null)
        {
            return DeliveryErrors.LocationNotFound;
        }

        location.SetLocation(new GeoPoint(request.Latitude, request.Longitude));
        await db.SaveChangesAsync(cancellationToken);
        return location.ToDto();
    }

    public Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeActiveAsync(id, l => l.Deactivate(), cancellationToken);

    public Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken) =>
        ChangeActiveAsync(id, l => l.Activate(), cancellationToken);

    private async Task<Result> ChangeActiveAsync(Guid id, Action<RouteLocation> change, CancellationToken cancellationToken)
    {
        var location = await db.RouteLocations.SingleOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (location is null)
        {
            return DeliveryErrors.LocationNotFound;
        }

        change(location);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private async Task TryGeocodeAsync(RouteLocation location, CancellationToken cancellationToken)
    {
        var result = await geocoding.GeocodeAsync(
            new GeocodingRequest(location.Street, location.BuildingNumber, location.City, location.PostalCode, location.Country),
            cancellationToken);
        if (result is not null)
        {
            location.SetLocation(result.Location);
        }
    }

    private Task<bool> IsNameTakenAsync(string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.RouteLocations.AnyAsync(l => l.Name == name && l.Id != exceptId, cancellationToken);

    private static RouteLocationDetails ToDetails(RouteLocationRequest request) =>
        new(request.Name, request.Street, request.BuildingNumber, request.City, request.PostalCode, request.Country);
}
