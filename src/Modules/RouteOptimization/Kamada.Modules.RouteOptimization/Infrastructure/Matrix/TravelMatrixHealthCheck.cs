using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>
/// Asks the configured (primary, uncached) provider for a tiny matrix. Reported as <c>Degraded</c> on failure,
/// because optimization still works with the approximate fallback. The outcome is cached for a few minutes so
/// frequent health probes do not hammer a public OSRM server.
/// </summary>
internal sealed class TravelMatrixHealthCheck(
    [FromKeyedServices(TravelMatrixHealthCheck.PrimaryProviderKey)] ITravelTimeMatrixProvider primary,
    IMemoryCache cache) : IHealthCheck
{
    public const string PrimaryProviderKey = "route-optimization:primary-matrix-provider";

    private const string CacheKey = "route-optimization:health";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    // Two points in Gdańsk (Long Street → main railway station), covered by the default self-hosted OSRM extract.
    private static readonly GeoPoint[] Probe = [new(54.34850, 18.65266), new(54.35565, 18.64393)];

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (cache.TryGetValue(CacheKey, out HealthCheckResult cached))
        {
            return cached;
        }

        HealthCheckResult result;
        try
        {
            var matrix = await primary.GetMatrixAsync(Probe, cancellationToken);
            result = HealthCheckResult.Healthy($"{matrix.Provider}{(matrix.IsApproximate ? " (approximate)" : string.Empty)}");
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            result = new HealthCheckResult(context.Registration.FailureStatus, "Travel-time matrix provider unavailable – using the approximate fallback.", ex);
        }

        cache.Set(CacheKey, result, CacheDuration);
        return result;
    }
}
