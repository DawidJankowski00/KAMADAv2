using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Shared.Contracts.Delivery;
using ContractRunStatus = Kamada.Shared.Contracts.Delivery.DeliveryRunStatus;
using ContractStopStatus = Kamada.Shared.Contracts.Delivery.DeliveryStopStatus;
using DomainRunStatus = Kamada.Modules.Delivery.Domain.Runs.DeliveryRunStatus;

namespace Kamada.Modules.Delivery.Application;

internal static class DeliveryMappings
{
    public static RouteLocationDto ToDto(this RouteLocation location) => new(
        location.Id,
        location.Name,
        location.Street,
        location.BuildingNumber,
        location.City,
        location.PostalCode,
        location.Country,
        location.Latitude,
        location.Longitude,
        location.IsActive,
        location.CreatedAt);

    public static ContractRunStatus ToContract(this DomainRunStatus status) => Enum.Parse<ContractRunStatus>(status.ToString());

    public static DomainRunStatus ToDomain(this ContractRunStatus status) => Enum.Parse<DomainRunStatus>(status.ToString());

    public static ContractStopStatus ToContract(this Domain.Runs.DeliveryStopStatus status) => Enum.Parse<ContractStopStatus>(status.ToString());

    public static NavigationLinkDto ToDto(this RunNavigationLink link) =>
        new(link.Part, link.TotalParts, link.FromPoint, link.ToPoint, link.Url);

    public static RunNavigationLink ToRunLink(this NavigationLink link) =>
        new(link.Part, link.TotalParts, link.FromIndex, link.ToIndex, link.Url);

    public static RunRoutePlanDto? ToRoutePlanDto(this DeliveryRun run) =>
        run.OptimizedAt is { } plannedAt
            ? new RunRoutePlanDto(
                run.OptimizedDurationSeconds ?? 0,
                run.OptimizedDistanceMeters ?? 0,
                run.OptimizationAlgorithm ?? string.Empty,
                run.RouteIsOptimal,
                run.RouteIsApproximate,
                run.MatrixProvider ?? string.Empty,
                plannedAt)
            : null;
}
