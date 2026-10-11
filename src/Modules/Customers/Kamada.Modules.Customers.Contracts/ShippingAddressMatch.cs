using Kamada.Shared.Matching.Addresses;

namespace Kamada.Modules.Customers.Contracts;

/// <summary>An existing active shipping address that matches an <see cref="AddressMention"/>.</summary>
public sealed record ShippingAddressMatch(
    Guid AddressId,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude);
