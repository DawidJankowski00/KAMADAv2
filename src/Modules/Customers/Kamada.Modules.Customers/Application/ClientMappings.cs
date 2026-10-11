using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Contracts.Customers;
using ContractAddressType = Kamada.Shared.Contracts.Customers.AddressType;
using ContractCoordinatesSource = Kamada.Shared.Contracts.Customers.CoordinatesSource;
using DomainAddressType = Kamada.Modules.Customers.Domain.Clients.AddressType;
using DomainCoordinatesSource = Kamada.Modules.Customers.Domain.Clients.CoordinatesSource;

namespace Kamada.Modules.Customers.Application;

internal static class ClientMappings
{
    public static ClientDto ToDto(this Client client) => new(
        client.Id,
        client.Username,
        client.Email,
        client.Name,
        client.Surname,
        client.PhoneNumber,
        client.IsActive,
        client.CreatedAt,
        [.. client.ActiveAddresses.OrderBy(a => a.Type).ThenBy(a => a.CreatedAt).Select(a => a.ToDto())]);

    public static ClientLookupDto ToLookupDto(this Client client) => new(
        client.Id,
        client.Username,
        client.Email,
        client.Name,
        client.Surname,
        client.PhoneNumber,
        [.. client.ActiveAddresses.Where(a => a.Type == DomainAddressType.Shipping).OrderBy(a => a.CreatedAt).Select(a => a.ToDto())]);

    public static AddressDto ToDto(this Address address) => new(
        address.Id,
        address.Type.ToContract(),
        address.Street,
        address.BuildingNumber,
        address.ApartmentNumber,
        address.City,
        address.PostalCode,
        address.Country,
        address.Latitude,
        address.Longitude,
        address.CoordinatesSource switch
        {
            DomainCoordinatesSource.Geocoded => ContractCoordinatesSource.Geocoded,
            DomainCoordinatesSource.Manual => ContractCoordinatesSource.Manual,
            _ => null,
        },
        address.GeocodedAt);

    public static AddressDetails ToDomain(this AddressRequest request) => new(
        request.Type.ToDomain(),
        request.Street,
        request.BuildingNumber,
        request.ApartmentNumber,
        request.City,
        request.PostalCode,
        request.Country);

    public static ContractAddressType ToContract(this DomainAddressType type) => type switch
    {
        DomainAddressType.Shipping => ContractAddressType.Shipping,
        DomainAddressType.Billing => ContractAddressType.Billing,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };

    public static DomainAddressType ToDomain(this ContractAddressType type) => type switch
    {
        ContractAddressType.Shipping => DomainAddressType.Shipping,
        ContractAddressType.Billing => DomainAddressType.Billing,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}
