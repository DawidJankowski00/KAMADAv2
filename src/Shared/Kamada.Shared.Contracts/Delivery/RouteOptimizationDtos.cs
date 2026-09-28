namespace Kamada.Shared.Contracts.Delivery;

/// <param name="LegDurationSeconds">Driving time from the previous point (start location for the first stop).</param>
/// <param name="ArrivalOffsetSeconds">Estimated arrival, counted from the start of the run (includes service time at earlier stops).</param>
public sealed record PlannedStopDto(
    Guid OrderId,
    long OrderNumber,
    int Sequence,
    double LegDurationSeconds,
    double LegDistanceMeters,
    double ArrivalOffsetSeconds,
    double Latitude,
    double Longitude);

/// <param name="ReturnLegDurationSeconds">Driving time from the last stop to the end location.</param>
public sealed record RoutePlanDto(
    IReadOnlyList<PlannedStopDto> Stops,
    double TotalDurationSeconds,
    double TotalDistanceMeters,
    double ReturnLegDurationSeconds,
    double ReturnLegDistanceMeters,
    string Algorithm,
    bool IsOptimal,
    bool IsApproximate,
    string MatrixProvider,
    IReadOnlyList<NavigationLinkDto> NavigationLinks);

/// <summary>Result of "optimize" (nothing is saved): the proposal and the current order on the same matrix.</summary>
public sealed record RouteOptimizationPreviewDto(RoutePlanDto Optimized, RoutePlanDto Current)
{
    public double SavedSeconds => Current.TotalDurationSeconds - Optimized.TotalDurationSeconds;

    public double SavedMeters => Current.TotalDistanceMeters - Optimized.TotalDistanceMeters;
}
