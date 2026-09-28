using Kamada.Modules.RouteOptimization.Domain;
using Kamada.Modules.RouteOptimization.Domain.Solvers;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Modules.RouteOptimization.UnitTests.Solvers;

/// <summary>Small cases with a hand-computed optimum, run against every solver.</summary>
public sealed class SolverTests
{
    public static TheoryData<string> SolverNames =>
        [BruteForceSolver.AlgorithmName, HeldKarpSolver.AlgorithmName, NearestNeighborTwoOptSolver.AlgorithmName];

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void Solve_AsymmetricMatrix_UsesCheaperDirection(string solverName)
    {
        // 0 = start, 3 = end. 0→2 is the nearest first hop, but 2→1 is a long detour (one-way street):
        // [1, 2] = 10 + 5 + 10 = 25, [2, 1] = 1 + 50 + 1 = 52.
        var matrix = Matrices.FromDurations(new double[,]
        {
            { 0, 10, 1, 99 },
            { 99, 0, 5, 1 },
            { 99, 50, 0, 10 },
            { 99, 99, 99, 0 },
        });

        var solution = Create(solverName).Solve(matrix, 0, 3);

        solution.StopOrder.ShouldBe([1, 2]);
        solution.TotalDurationSeconds.ShouldBe(25);
        solution.TotalDistanceMeters.ShouldBe(250);
    }

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void Solve_RoundTripWithSameStartAndEnd_FollowsOneWayCycle(string solverName)
    {
        // Only the cycle 0→1→2→3→0 is cheap (1 per hop); everything else costs 10.
        var d = new double[4, 4];
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                d[i, j] = i == j ? 0 : j == (i + 1) % 4 ? 1 : 10;
            }
        }

        var solution = Create(solverName).Solve(Matrices.FromDurations(d), 0, 0);

        solution.StopOrder.ShouldBe([1, 2, 3]);
        solution.TotalDurationSeconds.ShouldBe(4);
    }

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void Solve_PointsOnALine_VisitsThemInOrder(string solverName)
    {
        // Matrix index → position on a line; start at 0 (index 0), end at 7 (index 7). Optimum = 7.
        int[] position = [0, 5, 2, 6, 1, 4, 3, 7];
        var d = new double[8, 8];
        for (var i = 0; i < 8; i++)
        {
            for (var j = 0; j < 8; j++)
            {
                d[i, j] = Math.Abs(position[i] - position[j]);
            }
        }

        var solution = Create(solverName).Solve(Matrices.FromDurations(d), 0, 7);

        solution.StopOrder.ShouldBe([4, 2, 6, 5, 1, 3]);
        solution.TotalDurationSeconds.ShouldBe(7);
    }

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void Solve_NoStops_GoesStraightToEnd(string solverName)
    {
        var matrix = Matrices.FromDurations(new double[,] { { 0, 42 }, { 7, 0 } });

        var solution = Create(solverName).Solve(matrix, 0, 1);

        solution.StopOrder.ShouldBeEmpty();
        solution.TotalDurationSeconds.ShouldBe(42);
    }

    [Theory]
    [MemberData(nameof(SolverNames))]
    public void Solve_EndBeforeStartInMatrix_TreatsIndexesAsGiven(string solverName)
    {
        var matrix = Matrices.FromDurations(new double[,]
        {
            { 0, 3, 8 },
            { 4, 0, 2 },
            { 5, 6, 0 },
        });

        // start = 2, end = 0, single stop 1: 2→1→0 = 6 + 4.
        var solution = Create(solverName).Solve(matrix, 2, 0);

        solution.StopOrder.ShouldBe([1]);
        solution.TotalDurationSeconds.ShouldBe(10);
    }

    [Fact]
    public void Solve_ExactSolvers_ReportOptimal()
    {
        var matrix = Matrices.Random(6, seed: 1);

        new BruteForceSolver().Solve(matrix, 0, 5).IsOptimal.ShouldBeTrue();
        new HeldKarpSolver().Solve(matrix, 0, 5).IsOptimal.ShouldBeTrue();
        CreateHeuristic().Solve(matrix, 0, 5).IsOptimal.ShouldBeFalse();
    }

    [Fact]
    public void Solve_BruteForceAboveLimit_Throws()
    {
        var matrix = Matrices.Random(BruteForceSolver.MaxStops + 3, seed: 1);

        Should.Throw<ArgumentException>(() => new BruteForceSolver().Solve(matrix, 0, matrix.Size - 1));
    }

    [Fact]
    public void Solve_HeldKarpAboveLimit_Throws()
    {
        var matrix = Matrices.Random(HeldKarpSolver.MaxStops + 3, seed: 1);

        Should.Throw<ArgumentException>(() => new HeldKarpSolver().Solve(matrix, 0, matrix.Size - 1));
    }

    [Fact]
    public void Solve_InvalidStartIndex_Throws()
    {
        var matrix = Matrices.Random(4, seed: 1);

        Should.Throw<ArgumentOutOfRangeException>(() => new HeldKarpSolver().Solve(matrix, 4, 0));
    }

    internal static IRouteSolver Create(string name) => name switch
    {
        BruteForceSolver.AlgorithmName => new BruteForceSolver(),
        HeldKarpSolver.AlgorithmName => new HeldKarpSolver(),
        _ => CreateHeuristic(),
    };

    /// <summary>Fake clock never advances, so the heuristic always runs local search to convergence (deterministic).</summary>
    internal static NearestNeighborTwoOptSolver CreateHeuristic() => new(TimeSpan.FromSeconds(1), new FakeTimeProvider());
}
