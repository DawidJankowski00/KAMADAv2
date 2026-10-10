using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kamada.Shared.Kernel.Geography;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace Kamada.Shared.Infrastructure.Geocoding;

/// <summary>
/// Google Geocoding API. Active only when <c>Geocoding:Provider = Google</c> and an API key is configured.
/// </summary>
internal sealed class GoogleGeocodingService(HttpClient http, IOptions<GeocodingOptions> options) : IGeocodingService
{
    public const string ProviderName = "Google";

    public async Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        var url = QueryHelpers.AddQueryString("maps/api/geocode/json", new Dictionary<string, string?>
        {
            ["address"] = request.ToQueryString(),
            ["language"] = "pl",
            ["key"] = options.Value.Google.ApiKey,
        });

        var response = await http.GetFromJsonAsync<GoogleResponse>(url, cancellationToken);
        var location = response?.Status == "OK" ? response.Results.FirstOrDefault()?.Geometry.Location : null;
        if (location is null || !GeoPoint.IsValid(location.Lat, location.Lng))
        {
            return null;
        }

        var result = response!.Results[0];
        return new GeocodingResult(
            new GeoPoint(location.Lat, location.Lng),
            ProviderName,
            result.FormattedAddress,
            new GeocodedAddress(
                Component(result, "route"),
                Component(result, "street_number"),
                Component(result, "postal_code"),
                Component(result, "locality") ?? Component(result, "postal_town")));
    }

    private static string? Component(GoogleResult result, string type) =>
        result.AddressComponents?.FirstOrDefault(c => c.Types.Contains(type, StringComparer.Ordinal))?.LongName;

    private sealed record GoogleResponse(
        [property: JsonPropertyName("status")] string Status,
        [property: JsonPropertyName("results")] GoogleResult[] Results);

    private sealed record GoogleResult(
        [property: JsonPropertyName("formatted_address")] string? FormattedAddress,
        [property: JsonPropertyName("geometry")] GoogleGeometry Geometry,
        [property: JsonPropertyName("address_components")] GoogleAddressComponent[]? AddressComponents);

    private sealed record GoogleAddressComponent(
        [property: JsonPropertyName("long_name")] string LongName,
        [property: JsonPropertyName("types")] string[] Types);

    private sealed record GoogleGeometry([property: JsonPropertyName("location")] GoogleLocation Location);

    private sealed record GoogleLocation(
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lng")] double Lng);
}
