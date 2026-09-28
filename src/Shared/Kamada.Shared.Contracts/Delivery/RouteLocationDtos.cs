namespace Kamada.Shared.Contracts.Delivery;

/// <summary>Start or end point of delivery runs (warehouse, courier base).</summary>
public sealed record RouteLocationDto(
    Guid Id,
    string Name,
    string Street,
    string BuildingNumber,
    string City,
    string PostalCode,
    string Country,
    double? Latitude,
    double? Longitude,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}

public sealed record RouteLocationRequest(
    string Name,
    string Street,
    string BuildingNumber,
    string City,
    string PostalCode,
    string Country = "Polska");

public sealed record SetRouteLocationPinRequest(double Latitude, double Longitude);
