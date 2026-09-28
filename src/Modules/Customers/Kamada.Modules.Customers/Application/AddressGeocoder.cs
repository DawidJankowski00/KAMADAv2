using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Infrastructure.Geocoding;

namespace Kamada.Modules.Customers.Application;

/// <summary>
/// Resolves coordinates for an address. A failed lookup leaves the address without coordinates
/// (the admin can place the pin manually); it never fails the save.
/// </summary>
internal sealed class AddressGeocoder(IGeocodingService geocoding, TimeProvider clock)
{
    public async Task<bool> TryGeocodeAsync(Address address, CancellationToken cancellationToken)
    {
        var result = await geocoding.GeocodeAsync(
            new GeocodingRequest(address.Street, address.BuildingNumber, address.City, address.PostalCode, address.Country),
            cancellationToken);

        if (result is null)
        {
            return false;
        }

        address.SetGeocodedLocation(result.Location, clock.GetUtcNow());
        return true;
    }
}
