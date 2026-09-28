namespace Kamada.Modules.RouteOptimization.Domain.Solvers;

/// <summary>
/// Picks the solver by the number of stops: brute force for tiny runs, Held–Karp while it is fast,
/// the heuristic above that.
/// </summary>
internal sealed class RouteSolverSelector
{
    private readonly int _bruteForceMaxStops;
    private readonly int _heldKarpMaxStops;
    private readonly IRouteSolver _bruteForce = new BruteForceSolver();
    private readonly IRouteSolver _heldKarp = new HeldKarpSolver();
    private readonly IRouteSolver _heuristic;

    public RouteSolverSelector(int bruteForceMaxStops, int heldKarpMaxStops, IRouteSolver heuristic)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(bruteForceMaxStops);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(bruteForceMaxStops, BruteForceSolver.MaxStops);
        ArgumentOutOfRangeException.ThrowIfLessThan(heldKarpMaxStops, bruteForceMaxStops);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(heldKarpMaxStops, HeldKarpSolver.MaxStops);
        ArgumentNullException.ThrowIfNull(heuristic);

        _bruteForceMaxStops = bruteForceMaxStops;
        _heldKarpMaxStops = heldKarpMaxStops;
        _heuristic = heuristic;
    }

    public IRouteSolver Select(int stopCount) => stopCount switch
    {
        _ when stopCount <= _bruteForceMaxStops => _bruteForce,
        _ when stopCount <= _heldKarpMaxStops => _heldKarp,
        _ => _heuristic,
    };

    public RouteSolution Solve(TravelMatrix matrix, int startIndex, int endIndex) =>
        Select(RouteProblem.Stops(matrix, startIndex, endIndex).Length).Solve(matrix, startIndex, endIndex);
}
