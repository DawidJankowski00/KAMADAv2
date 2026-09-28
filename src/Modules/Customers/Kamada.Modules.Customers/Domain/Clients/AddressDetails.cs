namespace Kamada.Modules.Customers.Domain.Clients;

internal enum AddressType
{
    Shipping,
    Billing,
}

internal enum CoordinatesSource
{
    Geocoded,
    Manual,
}

/// <summary>
/// Postal part of an address (value object). Trimmed on creation so comparisons are reliable.
/// </summary>
internal sealed record AddressDetails
{
    public AddressDetails(
        AddressType type,
        string street,
        string buildingNumber,
        string? apartmentNumber,
        string city,
        string postalCode,
        string country)
    {
        Type = type;
        Street = street.Trim();
        BuildingNumber = buildingNumber.Trim();
        ApartmentNumber = string.IsNullOrWhiteSpace(apartmentNumber) ? null : apartmentNumber.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    public AddressType Type { get; }

    public string Street { get; }

    public string BuildingNumber { get; }

    public string? ApartmentNumber { get; }

    public string City { get; }

    public string PostalCode { get; }

    public string Country { get; }

    /// <summary>
    /// True when both describe the same building – type and apartment number do not move the pin,
    /// so changing only them keeps the coordinates.
    /// </summary>
    public bool IsSameBuildingAs(AddressDetails other) =>
        string.Equals(Street, other.Street, StringComparison.OrdinalIgnoreCase)
        && string.Equals(BuildingNumber, other.BuildingNumber, StringComparison.OrdinalIgnoreCase)
        && string.Equals(City, other.City, StringComparison.OrdinalIgnoreCase)
        && string.Equals(PostalCode, other.PostalCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Country, other.Country, StringComparison.OrdinalIgnoreCase);
}
