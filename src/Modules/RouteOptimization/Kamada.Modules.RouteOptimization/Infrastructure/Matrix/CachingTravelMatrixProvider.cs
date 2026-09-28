using System.Globalization;
using System.Text;
using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>
/// Caches matrices by the <b>set</b> of points: coordinates are rounded to 5 decimals (~1 m), de-duplicated and
/// sorted, so the same stops in any order (optimize, then evaluate a manual order) hit one entry. The inner provider
/// is always asked for the canonical point list; the answer is re-indexed to the caller's order.
/// Approximate matrices are not cached, so the real provider is retried as soon as it recovers.
/// </summary>
internal sealed class CachingTravelMatrixProvider(
    ITravelTimeMatrixProvider inner,
    IMemoryCache cache,
    IOptions<RouteOptimizationOptions> options) : ITravelTimeMatrixProvider
{
    private const int Decimals = 5;

    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        var rounded = points.Select(Round).ToArray();
        var canonical = rounded.Distinct().OrderBy(p => p.Latitude).ThenBy(p => p.Longitude).ToArray();
        var key = CacheKey(canonical);

        if (!cache.TryGetValue(key, out TravelMatrix? matrix) || matrix is null)
        {
            matrix = await inner.GetMatrixAsync(canonical, cancellationToken);
            if (!matrix.IsApproximate)
            {
                cache.Set(key, matrix, options.Value.MatrixCacheDuration);
            }
        }

        var position = canonical.Select((p, i) => (p, i)).ToDictionary(x => x.p, x => x.i);
        return Reindex(matrix, [.. rounded.Select(p => position[p])]);
    }

    public static string CacheKey(IEnumerable<GeoPoint> canonicalPoints)
    {
        var key = new StringBuilder("travel-matrix:");
        foreach (var point in canonicalPoints)
        {
            key.Append(CultureInfo.InvariantCulture, $"{point.Latitude:F5},{point.Longitude:F5};");
        }

        return key.ToString();
    }

    private static GeoPoint Round(GeoPoint point) =>
        new(Math.Round(point.Latitude, Decimals), Math.Round(point.Longitude, Decimals));

    private static TravelMatrix Reindex(TravelMatrix source, int[] map)
    {
        var n = map.Length;
        var durations = new double[n, n];
        var distances = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                durations[i, j] = source.Duration(map[i], map[j]);
                distances[i, j] = source.Distance(map[i], map[j]);
            }
        }

        return new TravelMatrix(durations, distances, source.Provider, source.IsApproximate);
    }
}
