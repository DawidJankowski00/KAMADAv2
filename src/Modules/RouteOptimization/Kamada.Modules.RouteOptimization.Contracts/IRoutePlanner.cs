using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>
/// Orders stops so that the total driving time start → stops → end is minimal (TSP with fixed endpoints).
/// Travel times come from one matrix request; the order is solved locally.
/// </summary>
public interface IRoutePlanner
{
    /// <param name="currentOrder">Optional current order of stops (indexes into <see cref="RoutePlanRequest.Stops"/>) to compare with.</param>
    Task<Result<RouteOptimizationResult>> OptimizeAsync(
        RoutePlanRequest request,
        IReadOnlyList<int>? currentOrder,
        CancellationToken cancellationToken);

    /// <summary>Durations and distances of a given (e.g. manually arranged) order of stops.</summary>
    Task<Result<RoutePlan>> EvaluateAsync(
        RoutePlanRequest request,
        IReadOnlyList<int> stopOrder,
        CancellationToken cancellationToken);
}
