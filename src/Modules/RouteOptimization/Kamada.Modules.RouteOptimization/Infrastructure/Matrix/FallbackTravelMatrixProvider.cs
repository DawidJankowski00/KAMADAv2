using System.Text.Json;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Kamada.Modules.RouteOptimization.Infrastructure.Matrix;

/// <summary>
/// Falls back to the straight-line estimate when the road-network provider is down, so planning a run never fails
/// because of an external service. The result is flagged approximate and the UI says so.
/// </summary>
internal sealed partial class FallbackTravelMatrixProvider(
    ITravelTimeMatrixProvider primary,
    HaversineMatrixProvider fallback,
    ILogger<FallbackTravelMatrixProvider> logger) : ITravelTimeMatrixProvider
{
    public async Task<TravelMatrix> GetMatrixAsync(IReadOnlyList<GeoPoint> points, CancellationToken cancellationToken)
    {
        try
        {
            return await primary.GetMatrixAsync(points, cancellationToken);
        }
        catch (Exception ex) when (IsProviderFailure(ex, cancellationToken))
        {
            LogFallback(logger, ex, points.Count);
            return await fallback.GetMatrixAsync(points, cancellationToken);
        }
    }

    private static bool IsProviderFailure(Exception ex, CancellationToken cancellationToken) => ex switch
    {
        HttpRequestException or TimeoutRejectedException or BrokenCircuitException
            or TravelMatrixProviderException or JsonException => true,
        OperationCanceledException => !cancellationToken.IsCancellationRequested, // HttpClient timeout
        _ => false,
    };

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "Travel-time matrix provider failed for {PointCount} points, using the approximate straight-line estimate")]
    private static partial void LogFallback(ILogger logger, Exception exception, int pointCount);
}
