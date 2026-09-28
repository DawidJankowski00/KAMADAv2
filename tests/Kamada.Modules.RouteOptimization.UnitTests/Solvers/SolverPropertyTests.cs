using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Domain.Solvers;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Modules.RouteOptimization.UnitTests.Solvers;

/// <summary>Properties checked on many random asymmetric matrices (fixed seeds – deterministic).</summary>
public sealed class SolverPropertyTests
{
    private const double Tolerance = 1e-6;

    [Fact]
    public void HeldKarp_RandomAsymmetricMatrices_MatchesBruteForce()
    {
        var bruteForce = new BruteForceSolver();
        var heldKarp = new HeldKarpSolver();

        for (var seed = 0; seed < 300; seed++)
        {
            var stops = seed % 9; // 0..8 stops
            var roundTrip = seed % 2 == 0;
            var matrix = Matrices.Random(roundTrip ? stops + 1 : stops + 2, seed);
            var end = roundTrip ? 0 : matrix.Size - 1;

            var exact = bruteForce.Solve(matrix, 0, end);
            var dynamic = heldKarp.Solve(matrix, 0, end);

            dynamic.TotalDurationSeconds.ShouldBe(exact.TotalDurationSeconds, Tolerance, $"seed {seed}");
            RouteProblem.IsPermutationOfStops(matrix, 0, end, dynamic.StopOrder).ShouldBeTrue($"seed {seed}");
            RouteProblem.Duration(matrix, 0, end, dynamic.StopOrder).ShouldBe(dynamic.TotalDurationSeconds, Tolerance);
        }
    }

    [Fact]
    public void Heuristic_RandomMatrices_NeverWorseThanInputOrder()
    {
        var heuristic = SolverTests.CreateHeuristic();

        for (var seed = 0; seed < 200; seed++)
        {
            var size = 3 + (seed % 60);
            var matrix = Matrices.Random(size, seed);
            var end = size - 1;
            var inputOrder = RouteProblem.Stops(matrix, 0, end);

            var solution = heuristic.Solve(matrix, 0, end);

            RouteProblem.IsPermutationOfStops(matrix, 0, end, solution.StopOrder).ShouldBeTrue($"seed {seed}");
            solution.TotalDurationSeconds.ShouldBeLessThanOrEqualTo(
                RouteProblem.Duration(matrix, 0, end, inputOrder) + Tolerance, $"seed {seed}");
        }
    }

    [Fact]
    public void Heuristic_ZeroTimeBudget_StillReturnsValidRouteNotWorseThanInput()
    {
        var clock = new FakeTimeProvider();
        var heuristic = new NearestNeighborTwoOptSolver(TimeSpan.FromTicks(1), new AdvancingClock(clock));
        var matrix = Matrices.Random(40, seed: 7);
        var inputOrder = RouteProblem.Stops(matrix, 0, 39);

        var solution = heuristic.Solve(matrix, 0, 39);

        RouteProblem.IsPermutationOfStops(matrix, 0, 39, solution.StopOrder).ShouldBeTrue();
        solution.TotalDurationSeconds.ShouldBeLessThanOrEqualTo(RouteProblem.Duration(matrix, 0, 39, inputOrder));
    }

    [Fact]
    public void Heuristic_TwelveStops_StaysCloseToOptimum()
    {
        var heuristic = SolverTests.CreateHeuristic();
        var heldKarp = new HeldKarpSolver();
        var gaps = new List<double>();

        for (var seed = 0; seed < 40; seed++)
        {
            var matrix = Matrices.Random(14, seed);
            var optimum = heldKarp.Solve(matrix, 0, 13).TotalDurationSeconds;
            var approx = heuristic.Solve(matrix, 0, 13).TotalDurationSeconds;

            approx.ShouldBeGreaterThanOrEqualTo(optimum - Tolerance);
            gaps.Add((approx - optimum) / optimum);
        }

        gaps.Average().ShouldBeLessThan(0.05);
    }

    /// <summary>Every timestamp read moves time forward by a second, so any time limit expires immediately.</summary>
    private sealed class AdvancingClock(FakeTimeProvider inner) : TimeProvider
    {
        public override long TimestampFrequency => inner.TimestampFrequency;

        public override long GetTimestamp()
        {
            inner.Advance(TimeSpan.FromSeconds(1));
            return inner.GetTimestamp();
        }
    }
}
