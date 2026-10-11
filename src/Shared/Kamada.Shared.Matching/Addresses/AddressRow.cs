namespace Kamada.Shared.Matching.Addresses;

/// <summary>Searchable projection of an active shipping address.</summary>
public sealed record AddressRow(
    Guid Id,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude);
