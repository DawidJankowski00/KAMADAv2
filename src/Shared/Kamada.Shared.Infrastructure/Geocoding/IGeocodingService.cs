using Kamada.Shared.Kernel.Geography;

namespace Kamada.Shared.Infrastructure.Geocoding;

public sealed record GeocodingRequest(string Street, string BuildingNumber, string City, string PostalCode, string Country)
{
    /// <summary>Normalized single-line form, used as cache key and for free-text providers.</summary>
    public string ToQueryString() =>
        $"{Street.Trim()} {BuildingNumber.Trim()}, {PostalCode.Trim()} {City.Trim()}, {Country.Trim()}";
}

public sealed record GeocodingResult(GeoPoint Location, string Provider, string? DisplayName);

/// <summary>
/// Converts a postal address into coordinates. Returns <c>null</c> when the address cannot be found
/// or the provider is unavailable – callers save the address without coordinates in that case.
/// </summary>
public interface IGeocodingService
{
    Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken);
}
