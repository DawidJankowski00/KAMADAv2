namespace Kamada.Modules.Delivery.Domain.Locations;

/// <summary>Name and postal address of a route location (value object, trimmed on creation).</summary>
internal sealed record RouteLocationDetails
{
    public RouteLocationDetails(string name, string street, string buildingNumber, string city, string postalCode, string country)
    {
        Name = name.Trim();
        Street = street.Trim();
        BuildingNumber = buildingNumber.Trim();
        City = city.Trim();
        PostalCode = postalCode.Trim();
        Country = country.Trim();
    }

    public string Name { get; }

    public string Street { get; }

    public string BuildingNumber { get; }

    public string City { get; }

    public string PostalCode { get; }

    public string Country { get; }

    /// <summary>Renaming a location does not move its pin; changing the building does.</summary>
    public bool IsSameBuildingAs(RouteLocationDetails other) =>
        string.Equals(Street, other.Street, StringComparison.OrdinalIgnoreCase)
        && string.Equals(BuildingNumber, other.BuildingNumber, StringComparison.OrdinalIgnoreCase)
        && string.Equals(City, other.City, StringComparison.OrdinalIgnoreCase)
        && string.Equals(PostalCode, other.PostalCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(Country, other.Country, StringComparison.OrdinalIgnoreCase);
}
