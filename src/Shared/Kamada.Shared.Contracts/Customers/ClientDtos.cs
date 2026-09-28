namespace Kamada.Shared.Contracts.Customers;

public enum AddressType
{
    Shipping,
    Billing,
}

public enum CoordinatesSource
{
    /// <summary>Resolved automatically by the geocoding provider.</summary>
    Geocoded,

    /// <summary>Pin placed manually by an administrator.</summary>
    Manual,
}

public sealed record AddressDto(
    Guid Id,
    AddressType Type,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    string Country,
    double? Latitude,
    double? Longitude,
    CoordinatesSource? CoordinatesSource,
    DateTimeOffset? GeocodedAt)
{
    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}

public sealed record ClientSummaryDto(
    Guid Id,
    string Username,
    string Email,
    string Name,
    string Surname,
    string PhoneNumber,
    bool IsActive,
    int AddressCount,
    DateTimeOffset CreatedAt);

public sealed record ClientDto(
    Guid Id,
    string Username,
    string Email,
    string Name,
    string Surname,
    string PhoneNumber,
    bool IsActive,
    DateTimeOffset CreatedAt,
    IReadOnlyList<AddressDto> Addresses);

public sealed record AddressRequest(
    AddressType Type,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    string Country = "Polska");

public sealed record CreateClientRequest(
    string Username,
    string Email,
    string Name,
    string Surname,
    string PhoneNumber,
    IReadOnlyList<AddressRequest>? Addresses = null);

public sealed record UpdateClientRequest(string Email, string Name, string Surname, string PhoneNumber);

public sealed record SetAddressLocationRequest(double Latitude, double Longitude);

public sealed record ClientListQuery(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);
