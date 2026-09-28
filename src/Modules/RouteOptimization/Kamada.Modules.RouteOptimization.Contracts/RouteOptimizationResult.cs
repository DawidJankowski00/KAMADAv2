namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>
/// The optimized route and, when a current order was given, that order evaluated on the same travel-time matrix
/// (so the two are directly comparable).
/// </summary>
public sealed record RouteOptimizationResult(RoutePlan Optimized, RoutePlan? Current);
