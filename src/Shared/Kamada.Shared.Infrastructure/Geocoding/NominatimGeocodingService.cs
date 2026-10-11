using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Matching.Text;
using Microsoft.AspNetCore.WebUtilities;

namespace Kamada.Shared.Infrastructure.Geocoding;

/// <summary>
/// OpenStreetMap Nominatim structured search. Usage policy (identifying User-Agent, ≤ 1 request/s, caching)
/// is enforced by the HTTP client configuration and <see cref="CachingGeocodingService"/>.
/// </summary>
internal sealed class NominatimGeocodingService(HttpClient http) : IGeocodingService
{
    public const string ProviderName = "Nominatim";

    private static readonly HashSet<string> StreetTypes =
        new(["al", "aleja", "aleje", "ul", "ulica", "pl", "plac", "os", "osiedle", "rondo", "skwer"], StringComparer.Ordinal);

    public async Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        // OSM names many main streets "aleja …" while people (and speech) drop the word: try that once more.
        return await SearchAsync(request, cancellationToken)
            ?? (StartsWithStreetType(request.Street)
                ? null
                : await SearchAsync(request with { Street = "aleja " + request.Street.Trim() }, cancellationToken));
    }

    /// <summary>"al. Grunwaldzka", "ulica Długa", "plac Wolności", "os. Przymorze" – the type is already there.</summary>
    private static bool StartsWithStreetType(string street) =>
        TextSimilarity.Tokens(street).FirstOrDefault() is { } first && StreetTypes.Contains(first);

    private async Task<GeocodingResult?> SearchAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        var query = new Dictionary<string, string?>
        {
            ["format"] = "jsonv2",
            ["limit"] = "1",
            ["addressdetails"] = "1",
            ["street"] = $"{request.BuildingNumber.Trim()} {request.Street.Trim()}".Trim(),
            ["city"] = request.City.Trim(),
            ["postalcode"] = request.PostalCode.Trim(),
            ["country"] = request.Country.Trim(),
            ["accept-language"] = "pl",
        };
        foreach (var empty in query.Where(p => string.IsNullOrEmpty(p.Value)).Select(p => p.Key).ToList())
        {
            query.Remove(empty);
        }

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

        var address = place.Address is { } a
            ? new GeocodedAddress(a.Road, a.HouseNumber, a.Postcode, a.City ?? a.Town ?? a.Village ?? a.Hamlet)
            : null;
        return new GeocodingResult(new GeoPoint(latitude, longitude), ProviderName, place.DisplayName, address);
    }

    private sealed record NominatimPlace(
        [property: JsonPropertyName("lat")] string Lat,
        [property: JsonPropertyName("lon")] string Lon,
        [property: JsonPropertyName("display_name")] string? DisplayName,
        [property: JsonPropertyName("address")] NominatimAddress? Address);

    private sealed record NominatimAddress(
        [property: JsonPropertyName("road")] string? Road,
        [property: JsonPropertyName("house_number")] string? HouseNumber,
        [property: JsonPropertyName("postcode")] string? Postcode,
        [property: JsonPropertyName("city")] string? City,
        [property: JsonPropertyName("town")] string? Town,
        [property: JsonPropertyName("village")] string? Village,
        [property: JsonPropertyName("hamlet")] string? Hamlet);
}
