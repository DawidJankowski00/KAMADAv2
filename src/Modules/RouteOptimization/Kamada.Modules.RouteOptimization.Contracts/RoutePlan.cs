namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>
/// A route through all stops of a <see cref="RoutePlanRequest"/>.
/// </summary>
/// <param name="StopOrder">Indexes into <see cref="RoutePlanRequest.Stops"/> in visiting order.</param>
/// <param name="Legs">Start → first stop, …, last stop → end (<c>StopOrder.Count + 1</c> legs).</param>
/// <param name="Algorithm">Solver that produced the order, or <c>Manual</c> for an evaluated order.</param>
/// <param name="IsOptimal">The order is proven optimal for the travel-time matrix used.</param>
/// <param name="MatrixProvider">Source of travel times (<c>OSRM</c>, <c>Google</c>, <c>Haversine</c>).</param>
/// <param name="IsApproximate">Travel times are straight-line estimates, not road network times.</param>
public sealed record RoutePlan(
    IReadOnlyList<int> StopOrder,
    IReadOnlyList<RouteLeg> Legs,
    double TotalDurationSeconds,
    double TotalDistanceMeters,
    string Algorithm,
    bool IsOptimal,
    string MatrixProvider,
    bool IsApproximate);
