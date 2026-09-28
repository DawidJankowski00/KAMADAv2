using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>
/// Offline estimate (straight line × circuity / average speed). Symmetric and always flagged as approximate.
/// Used by tests and as the fallback when the road-network provider is unavailable.
/// </summary>
internal sealed class HaversineMatrixProvider(IOptions<RouteOptimizationOptions> options) : ITravelTimeMatrixProvider
{
    public const string ProviderName = "Haversine";

    public Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(points);
        var settings = options.Value.Haversine;
        var n = points.Count;
        var durations = new double[n, n];
        var distances = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var (duration, distance) = StraightLineEstimate.Estimate(points[i], points[j], settings);
                durations[i, j] = durations[j, i] = duration;
                distances[i, j] = distances[j, i] = distance;
            }
        }

        return Task.FromResult(new TravelMatrix(durations, distances, ProviderName, isApproximate: true));
    }
}
