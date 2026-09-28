namespace Kamada.Modules.RouteOptimization.Domain.Solvers;

internal interface IRouteSolver
{
    string Name { get; }

    /// <summary>Finds the order of all stops (indexes other than start and end) minimising total duration.</summary>
    RouteSolution Solve(TravelMatrix matrix, int startIndex, int endIndex);
}
