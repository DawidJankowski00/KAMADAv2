using Kamada.Shared.Infrastructure.Geocoding;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Deterministic geocoder: streets containing <see cref="UnknownStreetMarker"/> are "not found",
/// every other address resolves to a point derived from the building number (so different buildings differ).
/// </summary>
internal sealed class FakeGeocodingService : IGeocodingService
{
    public const string UnknownStreetMarker = "Nieistniejąca";

    public static GeoPoint LocationFor(string buildingNumber) =>
        new(53.12 + (Math.Abs(buildingNumber.GetHashCode(StringComparison.Ordinal)) % 1000) / 100_000d, 18.00);

    public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(request.Street.Contains(UnknownStreetMarker, StringComparison.OrdinalIgnoreCase)
            ? null
            : new GeocodingResult(LocationFor(request.BuildingNumber), "Fake", request.ToQueryString()));
}
