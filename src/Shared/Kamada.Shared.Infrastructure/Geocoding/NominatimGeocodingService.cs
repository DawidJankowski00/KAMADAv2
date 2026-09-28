using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kamada.Shared.Kernel.Geography;
using Microsoft.AspNetCore.WebUtilities;

namespace Kamada.Shared.Infrastructure.Geocoding;

/// <summary>
/// OpenStreetMap Nominatim structured search. Usage policy (identifying User-Agent, ≤ 1 request/s, caching)
/// is enforced by the HTTP client configuration and <see cref="CachingGeocodingService"/>.
/// </summary>
internal sealed class NominatimGeocodingService(HttpClient http) : IGeocodingService
{
    public const string ProviderName = "Nominatim";

    public async Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["format"] = "jsonv2",
            ["limit"] = "1",
            ["street"] = $"{request.BuildingNumber.Trim()} {request.Street.Trim()}",
            ["city"] = request.City.Trim(),
            ["postalcode"] = request.PostalCode.Trim(),
            ["country"] = request.Country.Trim(),
            ["accept-language"] = "pl",
        };

        var places = await http.GetFromJsonAsync<NominatimPlace[]>(
            QueryHelpers.AddQueryString("search", query),
            cancellationToken);

        var place = places?.FirstOrDefault();
        if (place is null
            || !double.TryParse(place.Lat, NumberStyles.Float, CultureInfo.InvariantCulture, out var latitude)
            || !double.TryParse(place.Lon, NumberStyles.Float, CultureInfo.InvariantCulture, out var longitude)
            || !GeoPoint.IsValid(latitude, longitude))
        {
            return null;
        }

        return new GeocodingResult(new GeoPoint(latitude, longitude), ProviderName, place.DisplayName);
    }

    private sealed record NominatimPlace(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string? DisplayName);
}
