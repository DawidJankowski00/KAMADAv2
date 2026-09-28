namespace Kamada.Modules.RouteOptimization.Contracts;

/// <summary>One drive between two consecutive points of a route.</summary>
public sealed record RouteLeg(double DurationSeconds, double DistanceMeters);
