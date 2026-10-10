using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic geocoder: streets containing <see cref="UnknownStreetMarker"/> are "not found",
/// every other address resolves to a point derived from the building number (so different buildings differ).
/// A missing postal code is "found" as <see cref="PostalCode"/>.
/// </summary>
internal sealed class FakeGeocodingService : IGeocodingService
{
    public const string UnknownStreetMarker = "Nieistniejąca";

    public const string PostalCode = "80-001";

    public static GeoPoint LocationFor(string buildingNumber) =>
        new(53.12 + (Math.Abs(buildingNumber.GetHashCode(StringComparison.Ordinal)) % 1000) / 100_000d, 18.00);

    public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Street.Contains(UnknownStreetMarker, StringComparison.OrdinalIgnoreCase)
            ? null
            : new GeocodingResult(
                LocationFor(request.BuildingNumber),
                "Fake",
                request.ToQueryString(),
                new GeocodedAddress(
                    request.Street,
                    request.BuildingNumber,
                    string.IsNullOrWhiteSpace(request.PostalCode) ? PostalCode : request.PostalCode,
                    request.City)));
}
