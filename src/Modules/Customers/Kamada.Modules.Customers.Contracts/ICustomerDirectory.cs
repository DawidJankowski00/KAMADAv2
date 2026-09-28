using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Customers.Contracts;

/// <summary>Who and where to deliver to – copied into the order as a snapshot.</summary>
public sealed record ShippingDetails(
    Guid ClientId,
    string ClientName,
    string PhoneNumber,
    Guid AddressId,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    string Country,
    double? Latitude,
    double? Longitude);

/// <summary>Current coordinates of an address (may have been pinned after the order was placed).</summary>
public sealed record AddressLocation(Guid AddressId, double? Latitude, double? Longitude);

public interface ICustomerDirectory
{
    /// <summary>Fails when the client is unknown or inactive, or the address is not an active shipping address of that client.</summary>
    Task<Result<ShippingDetails>> GetShippingDetailsAsync(Guid clientId, Guid addressId, CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, AddressLocation>> GetAddressLocationsAsync(IReadOnlyCollection<Guid> addressIds, CancellationToken cancellationToken);
}
