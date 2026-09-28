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
/// OSRM table service: <c>GET /table/v1/{profile}/{lng,lat;…}?annotations=duration,distance</c> – all pairs in one call.
/// Pairs OSRM cannot route (<c>null</c>) are estimated in a straight line and the matrix is flagged approximate.
/// </summary>
internal sealed class OsrmMatrixProvider(HttpClient http, IOptions<RouteOptimizationOptions> options) : ITravelTimeMatrixProvider
{
    public const string ProviderName = "OSRM";

    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        var settings = options.Value;
        var coordinates = string.Join(';', points.Select(p =>
            string.Create(CultureInfo.InvariantCulture, $"{p.Longitude:0.######},{p.Latitude:0.######}")));

        var response = await http.GetFromJsonAsync<OsrmTableResponse>(
            $"table/v1/{settings.Osrm.Profile}/{coordinates}?annotations=duration,distance",
            cancellationToken);

        var n = points.Count;
        if (response is not { Code: "Ok", Durations: { } rawDurations, Distances: { } rawDistances }
            || rawDurations.Length != n || rawDistances.Length != n)
        {
            throw new TravelMatrixProviderException($"OSRM table failed: {response?.Code ?? "empty response"} {response?.Message}");
        }

        var durations = new double[n, n];
        var distances = new double[n, n];
        var estimated = false;
        for (var i = 0; i < n; i++)
        {
            if (rawDurations[i].Length != n || rawDistances[i].Length != n)
            {
                throw new TravelMatrixProviderException("OSRM table has rows of unexpected length.");
            }

            for (var j = 0; j < n; j++)
            {
                if (i == j)
                {
                    continue;
                }

                if (rawDurations[i][j] is { } duration && rawDistances[i][j] is { } distance)
                {
                    durations[i, j] = duration;
                    distances[i, j] = distance;
                }
                else
                {
                    (durations[i, j], distances[i, j]) = StraightLineEstimate.Estimate(points[i], points[j], settings.Haversine);
                    estimated = true;
                }
            }
        }

        return new TravelMatrix(durations, distances, ProviderName, estimated);
    }

    private sealed record OsrmTableResponse(
        [property: JsonPropertyName("code")] string? Code,
        [property: JsonPropertyName("message")] string? Message,
        [property: JsonPropertyName("durations")] double?[][]? Durations,
        [property: JsonPropertyName("distances")] double?[][]? Distances);
}
