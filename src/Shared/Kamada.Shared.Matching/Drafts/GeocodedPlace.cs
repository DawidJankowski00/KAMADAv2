namespace Kamada.Shared.Matching.Drafts;

/// <summary>What a geocoder found for a spoken address; the parts it did not return are <c>null</c>.</summary>
public sealed record GeocodedPlace(
    double Latitude,
    double Longitude,
    string? Street,
    string? BuildingNumber,
    string? PostalCode,
    string? City);
