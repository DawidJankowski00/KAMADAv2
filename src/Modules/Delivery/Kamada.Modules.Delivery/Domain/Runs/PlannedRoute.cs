using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Delivery.Domain.Runs;

/// <summary>One stop of a computed route, in visiting order.</summary>
internal sealed record PlannedStop(Guid OrderId, double ArrivalOffsetSeconds, GeoPoint Location);

/// <summary>A stop order with its metrics, computed by the route planner (optimized or manual).</summary>
internal sealed record PlannedRoute(
    IReadOnlyList<PlannedStop> Stops,
    double DurationSeconds,
    double DistanceMeters,
    string Algorithm,
    bool IsOptimal,
    bool IsApproximate,
    string MatrixProvider,
    IReadOnlyList<RunNavigationLink> NavigationLinks);
