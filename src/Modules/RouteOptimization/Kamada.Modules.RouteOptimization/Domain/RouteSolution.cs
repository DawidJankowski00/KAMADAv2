namespace Kamada.Modules.RouteOptimization.Domain;

/// <summary>
/// Visiting order of the stops (matrix indexes, start and end excluded) with its totals.
/// </summary>
/// <param name="IsOptimal">Proven optimal for the matrix (exact solver).</param>
internal sealed record RouteSolution(
    IReadOnlyList<int> StopOrder,
    double TotalDurationSeconds,
    double TotalDistanceMeters,
    string Algorithm,
    bool IsOptimal);
