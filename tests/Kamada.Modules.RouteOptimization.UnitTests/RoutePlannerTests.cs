using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Modules.RouteOptimization.Domain.Solvers;
using Kamada.Modules.RouteOptimization.Infrastructure.Matrix;
using Kamada.Modules.RouteOptimization.UnitTests.Solvers;
using Kamada.Shared.Kernel.Geography;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.RouteOptimization.UnitTests;

public sealed class RoutePlannerTests
{
    // Warehouse in the west, stops placed east of it along one street (by longitude).
    private static readonly GeoPoint Warehouse = new(53.12, 17.90);
    private static readonly GeoPoint Near = new(53.12, 17.95);
    private static readonly GeoPoint Middle = new(53.12, 18.00);
    private static readonly GeoPoint Far = new(53.12, 18.05);

    private static RoutePlanner Planner(int maxStops = 60)
    {
        var options = Options.Create(new RouteOptimizationOptions { MaxStops = maxStops });
        return new RoutePlanner(new HaversineMatrixProvider(options), new RouteSolverSelector(8, 15, SolverTests.CreateHeuristic()), options);
    }

    [Fact]
    public async Task OptimizeAsync_StopsGivenOutOfOrder_ReturnsIndexesIntoRequestStops()
    {
        var request = new RoutePlanRequest(Warehouse, Far, [Far, Near, Middle]);

        var result = await Planner().OptimizeAsync(request, currentOrder: null, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        var plan = result.Value.Optimized;
        plan.StopOrder.ShouldBe([1, 2, 0]); // Near, Middle, Far
        plan.Legs.Count.ShouldBe(4);
        plan.Legs[^1].DistanceMeters.ShouldBe(0); // last stop is the end point
        plan.TotalDurationSeconds.ShouldBe(plan.Legs.Sum(l => l.DurationSeconds), 1e-6);
        plan.Algorithm.ShouldBe(BruteForceSolver.AlgorithmName);
        plan.IsOptimal.ShouldBeTrue();
        plan.MatrixProvider.ShouldBe(HaversineMatrixProvider.ProviderName);
        plan.IsApproximate.ShouldBeTrue();
        result.Value.Current.ShouldBeNull();
    }

    [Fact]
    public async Task OptimizeAsync_WithCurrentOrder_ReturnsComparableCurrentPlan()
    {
        var request = new RoutePlanRequest(Warehouse, Warehouse, [Far, Near, Middle]);

        var result = await Planner().OptimizeAsync(request, [0, 1, 2], CancellationToken.None);

        var current = result.Value.Current.ShouldNotBeNull();
        current.StopOrder.ShouldBe([0, 1, 2]);
        current.Algorithm.ShouldBe(RoutePlanner.ManualAlgorithm);
        current.IsOptimal.ShouldBeFalse();
        result.Value.Optimized.TotalDurationSeconds.ShouldBeLessThan(current.TotalDurationSeconds);
    }

    [Fact]
    public async Task OptimizeAsync_NoStops_ReturnsDirectLeg()
    {
        var result = await Planner().OptimizeAsync(new RoutePlanRequest(Warehouse, Far, []), null, CancellationToken.None);

        result.Value.Optimized.StopOrder.ShouldBeEmpty();
        result.Value.Optimized.Legs.Count.ShouldBe(1);
    }

    [Fact]
    public async Task OptimizeAsync_TooManyStops_FailsWithValidationError()
    {
        var request = new RoutePlanRequest(Warehouse, Warehouse, [Near, Middle, Far]);

        var result = await Planner(maxStops: 2).OptimizeAsync(request, null, CancellationToken.None);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("RouteOptimization.TooManyStops");
    }

    [Theory]
    [InlineData(new[] { 0, 1 })]
    [InlineData(new[] { 0, 1, 1 })]
    [InlineData(new[] { 0, 1, 3 })]
    public async Task EvaluateAsync_OrderNotPermutationOfStops_Fails(int[] order)
    {
        var request = new RoutePlanRequest(Warehouse, Warehouse, [Near, Middle, Far]);

        var result = await Planner().EvaluateAsync(request, order, CancellationToken.None);

        result.Error.Code.ShouldBe("RouteOptimization.InvalidStopOrder");
    }

    [Fact]
    public async Task EvaluateAsync_ManualOrder_KeepsOrderAndSumsLegs()
    {
        var request = new RoutePlanRequest(Warehouse, Warehouse, [Near, Middle, Far]);

        var result = await Planner().EvaluateAsync(request, [2, 0, 1], CancellationToken.None);

        result.Value.StopOrder.ShouldBe([2, 0, 1]);
        result.Value.Legs.Count.ShouldBe(4);
        result.Value.TotalDistanceMeters.ShouldBe(result.Value.Legs.Sum(l => l.DistanceMeters), 1e-6);
    }
}
