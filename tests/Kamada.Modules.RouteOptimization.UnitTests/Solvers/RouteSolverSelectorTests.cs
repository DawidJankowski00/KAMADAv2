using Kamada.Modules.RouteOptimization.Domain.Solvers;

namespace Kamada.Modules.RouteOptimization.UnitTests.Solvers;

public sealed class RouteSolverSelectorTests
{
    private readonly RouteSolverSelector _selector = new(8, 15, SolverTests.CreateHeuristic());

    [Theory]
    [InlineData(0, BruteForceSolver.AlgorithmName)]
    [InlineData(8, BruteForceSolver.AlgorithmName)]
    [InlineData(9, HeldKarpSolver.AlgorithmName)]
    [InlineData(15, HeldKarpSolver.AlgorithmName)]
    [InlineData(16, NearestNeighborTwoOptSolver.AlgorithmName)]
    [InlineData(60, NearestNeighborTwoOptSolver.AlgorithmName)]
    public void Select_ByStopCount_PicksExpectedSolver(int stops, string expected) =>
        _selector.Select(stops).Name.ShouldBe(expected);

    [Fact]
    public void Solve_UsesSolverForMatrixStopCount()
    {
        var matrix = Matrices.Random(12, seed: 3); // 10 stops

        _selector.Solve(matrix, 0, 11).Algorithm.ShouldBe(HeldKarpSolver.AlgorithmName);
    }

    [Fact]
    public void Constructor_ThresholdsOutOfOrder_Throws() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new RouteSolverSelector(10, 9, SolverTests.CreateHeuristic()));
}
