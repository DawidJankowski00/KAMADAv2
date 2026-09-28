using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Domain.Solvers;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.Application;

/// <summary>
/// Matrix index layout: 0 = start, 1..n = request stops (stop k at index k + 1), n + 1 = end.
/// The end gets its own index even when it equals the start – its row/column is then simply a copy.
/// </summary>
internal sealed class RoutePlanner(
    ITravelTimeMatrixProvider matrixProvider,
    RouteSolverSelector solvers,
    IOptions<RouteOptimizationOptions> options) : IRoutePlanner
{
    public const string ManualAlgorithm = "Manual";

    public async Task<Result<RouteOptimizationResult>> OptimizeAsync(
        RoutePlanRequest request,
        IReadOnlyList<int>? currentOrder,
        CancellationToken cancellationToken)
    {
        var error = Validate(request, currentOrder);
        if (error is not null)
        {
            return error;
        }

        var matrix = await matrixProvider.GetMatrixAsync(Points(request), cancellationToken);
        var end = request.Stops.Count + 1;

        var solution = solvers.Solve(matrix, 0, end);
        var optimized = ToPlan(matrix, [.. solution.StopOrder.Select(i => i - 1)], solution.Algorithm, solution.IsOptimal);
        var current = currentOrder is null ? null : ToPlan(matrix, currentOrder, ManualAlgorithm, isOptimal: false);

        return new RouteOptimizationResult(optimized, current);
    }

    public async Task<Result<RoutePlan>> EvaluateAsync(
        RoutePlanRequest request,
        IReadOnlyList<int> stopOrder,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(stopOrder);
        var error = Validate(request, stopOrder);
        if (error is not null)
        {
            return error;
        }

        var matrix = await matrixProvider.GetMatrixAsync(Points(request), cancellationToken);
        return ToPlan(matrix, stopOrder, ManualAlgorithm, isOptimal: false);
    }

    private Error? Validate(RoutePlanRequest request, IReadOnlyList<int>? stopOrder)
    {
        ArgumentNullException.ThrowIfNull(request);
        var max = options.Value.MaxStops;
        if (request.Stops.Count > max)
        {
            return RouteOptimizationErrors.TooManyStops(request.Stops.Count, max);
        }

        if (stopOrder is not null && !stopOrder.Order().SequenceEqual(Enumerable.Range(0, request.Stops.Count)))
        {
            return RouteOptimizationErrors.InvalidStopOrder;
        }

        return null;
    }

    private static GeoPoint[] Points(RoutePlanRequest request) => [request.Start, .. request.Stops, request.End];

    private static RoutePlan ToPlan(TravelMatrix matrix, IReadOnlyList<int> stopOrder, string algorithm, bool isOptimal)
    {
        int[] path = [0, .. stopOrder.Select(i => i + 1), matrix.Size - 1];
        var legs = new RouteLeg[path.Length - 1];
        for (var k = 0; k < legs.Length; k++)
        {
            legs[k] = new RouteLeg(matrix.Duration(path[k], path[k + 1]), matrix.Distance(path[k], path[k + 1]));
        }

        return new RoutePlan(
            [.. stopOrder],
            legs,
            legs.Sum(l => l.DurationSeconds),
            legs.Sum(l => l.DistanceMeters),
            algorithm,
            isOptimal,
            matrix.Provider,
            matrix.IsApproximate);
    }
}
