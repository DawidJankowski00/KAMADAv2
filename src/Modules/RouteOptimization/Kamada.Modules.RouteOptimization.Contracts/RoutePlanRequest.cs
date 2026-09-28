using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>
/// A fixed start (e.g. warehouse), a fixed end (may equal the start) and the stops to visit in between.
/// Stops are identified by their position in <see cref="Stops"/>.
/// </summary>
public sealed record RoutePlanRequest(GeoPoint Start, GeoPoint End, IReadOnlyList<GeoPoint> Stops);
