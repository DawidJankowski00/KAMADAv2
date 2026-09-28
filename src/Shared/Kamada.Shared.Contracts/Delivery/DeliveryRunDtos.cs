using Kamada.Shared.Contracts.Ordering;

namespace Kamada.Shared.Contracts.Delivery;

public enum DeliveryRunStatus
{
    Draft,
    Planned,
    InProgress,
    Completed,
    Cancelled,
}

public enum DeliveryStopStatus
{
    Pending,
    Delivered,
    Failed,
}

/// <summary>Which of the signed-in courier's runs to list.</summary>
public enum CourierRunScope
{
    /// <summary>In progress, or planned for today or earlier.</summary>
    Current,

    /// <summary>Planned for a later day.</summary>
    Upcoming,

    /// <summary>Completed (most recent first).</summary>
    Completed,
}

public sealed record CourierSummaryDto(Guid Id, string FullName, string PhoneNumber);

/// <summary>
/// Google Maps link for a part of the route. Point indexes: 0 = start location, 1..n = stops in delivery
/// sequence, n + 1 = end location.
/// </summary>
public sealed record NavigationLinkDto(int Part, int TotalParts, int FromPoint, int ToPoint, string Url);

/// <summary>Metrics of the stored stop order (optimized or set manually).</summary>
public sealed record RunRoutePlanDto(
    double DurationSeconds,
    double DistanceMeters,
    string Algorithm,
    bool IsOptimal,
    bool IsApproximate,
    string MatrixProvider,
    DateTimeOffset PlannedAt);

public sealed record DeliveryStopItemDto(string ProductName, int Quantity);

public sealed record DeliveryStopDto(
    Guid Id,
    Guid OrderId,
    long OrderNumber,
    int Sequence,
    DeliveryStopStatus Status,
    OrderStatus OrderStatus,
    string RecipientName,
    string PhoneNumber,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude,
    double? EstimatedArrivalOffsetSeconds,
    string? FailureReason,
    DateTimeOffset? DeliveredAt,
    string? Notes,
    IReadOnlyList<DeliveryStopItemDto> Items,
    string? NavigationUrl)
{
    public bool HasCoordinates => Latitude is not null && Longitude is not null;
}

public sealed record DeliveryRunDto(
    Guid Id,
    string Name,
    DateOnly PlannedDate,
    DeliveryRunStatus Status,
    CourierSummaryDto Courier,
    string VehiclePlate,
    RouteLocationDto StartLocation,
    RouteLocationDto EndLocation,
    IReadOnlyList<DeliveryStopDto> Stops,
    RunRoutePlanDto? RoutePlan,
    IReadOnlyList<NavigationLinkDto> NavigationLinks,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public sealed record DeliveryRunSummaryDto(
    Guid Id,
    string Name,
    DateOnly PlannedDate,
    DeliveryRunStatus Status,
    Guid CourierId,
    string CourierName,
    string VehiclePlate,
    string StartLocationName,
    string EndLocationName,
    int StopCount,
    int DeliveredCount,
    int FailedCount,
    double? PlannedDurationSeconds,
    double? PlannedDistanceMeters);

public sealed record CreateDeliveryRunRequest(
    string Name,
    DateOnly PlannedDate,
    Guid CourierId,
    string VehiclePlate,
    Guid StartLocationId,
    Guid EndLocationId,
    IReadOnlyList<Guid>? OrderIds = null);

public sealed record UpdateDeliveryRunRequest(
    string Name,
    DateOnly PlannedDate,
    Guid CourierId,
    string VehiclePlate,
    Guid StartLocationId,
    Guid EndLocationId);

public sealed record AddRunOrdersRequest(IReadOnlyList<Guid> OrderIds);

/// <summary>Full stop order: every order of the run exactly once.</summary>
public sealed record SetRunSequenceRequest(IReadOnlyList<Guid> OrderIds);

public sealed record FailStopRequest(string Reason);

/// <param name="From">Planned on or after (inclusive).</param>
/// <param name="To">Planned on or before (inclusive).</param>
public sealed record DeliveryRunListQuery(
    DeliveryRunStatus? Status = null,
    Guid? CourierId = null,
    DateOnly? From = null,
    DateOnly? To = null,
    string? Search = null,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);

public sealed record CourierRunListQuery(CourierRunScope Scope = CourierRunScope.Current);
