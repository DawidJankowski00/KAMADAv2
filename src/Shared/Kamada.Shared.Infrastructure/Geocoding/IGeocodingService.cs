using Kamada.Shared.Kernel.Geography;

namespace Kamada.Shared.Infrastructure.Geocoding;

public sealed record GeocodingRequest(string Street, string BuildingNumber, string City, string PostalCode, string Country)
{
    /// <summary>Normalized single-line form, used as cache key and for free-text providers.</summary>
    public string ToQueryString() => string.Join(", ", new[]
        {
            $"{Street.Trim()} {BuildingNumber.Trim()}".Trim(),
            $"{PostalCode.Trim()} {City.Trim()}".Trim(),
            Country.Trim(),
        }.Where(part => part.Length > 0));
}

/// <summary>Postal parts of the found place, as the provider names them (any part may be missing).</summary>
public sealed record GeocodedAddress(string? Street, string? BuildingNumber, string? PostalCode, string? City);

/// <param name="Address">Structured address of the result; lets callers complete a partial address (e.g. the postal code).</param>
public sealed record GeocodingResult(GeoPoint Location, string Provider, string? DisplayName, GeocodedAddress? Address = null);

/// <summary>
/// Converts a postal address into coordinates. Empty parts (e.g. an unknown postal code) are left out of the query.
/// Returns <c>null</c> when the address cannot be found or the provider is unavailable – callers save the address
/// without coordinates in that case.
/// </summary>
public interface IGeocodingService
{
    Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken);
}
