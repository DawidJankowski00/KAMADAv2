using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>
/// Google Routes API <c>computeRouteMatrix</c> (official, paid API; active only with an API key from configuration).
/// One request allows at most 625 elements (25 × 25 without traffic-aware routing), so bigger matrices are
/// requested in tiles.
/// </summary>
internal sealed class GoogleRoutesMatrixProvider(HttpClient http, IOptions<RouteOptimizationOptions> options) : ITravelTimeMatrixProvider
{
    public const string ProviderName = "Google";

    private const int TileSize = 25;
    private const string FieldMask = "originIndex,destinationIndex,duration,distanceMeters,condition";

    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        var settings = options.Value;
        var n = points.Count;
        var durations = new double[n, n];
        var distances = new double[n, n];
        var found = new bool[n, n];

        for (var originOffset = 0; originOffset < n; originOffset += TileSize)
        {
            for (var destinationOffset = 0; destinationOffset < n; destinationOffset += TileSize)
            {
                var origins = points.Skip(originOffset).Take(TileSize).ToArray();
                var destinations = points.Skip(destinationOffset).Take(TileSize).ToArray();
                foreach (var element in await RequestTileAsync(origins, destinations, settings.Google.ApiKey!, cancellationToken))
                {
                    // Proto3 JSON omits zero values, so a missing index means 0.
                    var (o, d) = (element.OriginIndex ?? 0, element.DestinationIndex ?? 0);
                    if (element.Condition != "ROUTE_EXISTS" || o >= origins.Length || d >= destinations.Length)
                    {
                        continue;
                    }

                    var (i, j) = (originOffset + o, destinationOffset + d);
                    durations[i, j] = ParseDuration(element.Duration);
                    distances[i, j] = element.DistanceMeters ?? 0; // omitted by the API when zero
                    found[i, j] = true;
                }
            }
        }

        var estimated = false;
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                if (i != j && !found[i, j])
                {
                    (durations[i, j], distances[i, j]) = StraightLineEstimate.Estimate(points[i], points[j], settings.Haversine);
                    estimated = true;
                }
                else if (i == j)
                {
                    durations[i, j] = distances[i, j] = 0;
                }
            }
        }

        return new TravelMatrix(durations, distances, ProviderName, estimated);
    }

    private async Task<RouteMatrixElement[]> RequestTileAsync(
        GeoPoint[] origins, GeoPoint[] destinations, string apiKey, CancellationToken cancellationToken)
    {
        var body = new ComputeRouteMatrixRequest(
            [.. origins.Select(p => new RouteMatrixWaypoint(Waypoint.At(p)))],
            [.. destinations.Select(p => new RouteMatrixWaypoint(Waypoint.At(p)))],
            TravelMode: "DRIVE",
            RoutingPreference: "TRAFFIC_UNAWARE");

        using var request = new HttpRequestMessage(HttpMethod.Post, "distanceMatrix/v2:computeRouteMatrix")
        {
            Content = JsonContent.Create(body),
        };
        request.Headers.Add("X-Goog-Api-Key", apiKey);
        request.Headers.Add("X-Goog-FieldMask", FieldMask);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RouteMatrixElement[]>(cancellationToken)
            ?? throw new TravelMatrixProviderException("Google Routes returned an empty matrix.");
    }

    /// <summary>Durations are protobuf JSON strings such as <c>"754s"</c> or <c>"12.5s"</c>.</summary>
    private static double ParseDuration(string? value) =>
        value is not null
        && value.EndsWith('s')
        && double.TryParse(value.AsSpan(0, value.Length - 1), NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            ? seconds
            : throw new TravelMatrixProviderException($"Unexpected duration format: '{value}'.");

    private sealed record ComputeRouteMatrixRequest(
        [property: JsonPropertyName("origins")] RouteMatrixWaypoint[] Origins,
        [property: JsonPropertyName("destinations")] RouteMatrixWaypoint[] Destinations,
        [property: JsonPropertyName("travelMode")] string TravelMode,
        [property: JsonPropertyName("routingPreference")] string RoutingPreference);

    private sealed record RouteMatrixWaypoint([property: JsonPropertyName("waypoint")] Waypoint Waypoint);

    private sealed record Waypoint([property: JsonPropertyName("location")] Location Location)
    {
        public static Waypoint At(GeoPoint point) => new(new Location(new LatLng(point.Latitude, point.Longitude)));
    }

    private sealed record Location([property: JsonPropertyName("latLng")] LatLng LatLng);

    private sealed record LatLng(
        [property: JsonPropertyName("latitude")] double Latitude,
        [property: JsonPropertyName("longitude")] double Longitude);

    private sealed record RouteMatrixElement(
        [property: JsonPropertyName("originIndex")] int? OriginIndex,
        [property: JsonPropertyName("destinationIndex")] int? DestinationIndex,
        [property: JsonPropertyName("duration")] string? Duration,
        [property: JsonPropertyName("distanceMeters")] double? DistanceMeters,
        [property: JsonPropertyName("condition")] string? Condition);
}
