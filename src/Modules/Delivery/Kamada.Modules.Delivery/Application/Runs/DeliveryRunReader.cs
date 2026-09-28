using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Shared.Contracts.Delivery;
using Microsoft.EntityFrameworkCore;
using ContractOrderStatus = Kamada.Shared.Contracts.Ordering.OrderStatus;

namespace Kamada.Modules.Delivery.Application.Runs;

/// <summary>
/// Composes the full view of a run from this module (run, stops, locations) and the Ordering / Workforce /
/// Customers contracts (recipient, items, courier, current coordinates). Queries run one after another –
/// module contexts share the request's connection.
/// </summary>
internal sealed class DeliveryRunReader(
    IDeliveryDbContext db,
    IOrderFulfillment orders,
    ICourierDirectory couriers,
    StopLocationResolver locations,
    INavigationLinkBuilder navigation)
{
    public async Task<DeliveryRunDto> ReadAsync(DeliveryRun run, CancellationToken cancellationToken)
    {
        var routeLocations = await db.RouteLocations.AsNoTracking()
            .Where(l => l.Id == run.StartLocationId || l.Id == run.EndLocationId)
            .ToDictionaryAsync(l => l.Id, cancellationToken);

        var courier = (await couriers.GetCouriersAsync([run.CourierId], cancellationToken))
            .GetValueOrDefault(run.CourierId);

        var stops = run.Stops;
        var deliveryOrders = (await orders.GetDeliveryOrdersAsync([.. stops.Select(s => s.OrderId)], cancellationToken))
            .ToDictionary(o => o.OrderId);

        // Stops of a planned route keep the coordinates the route was computed with; others show current ones.
        var unplanned = stops.Where(s => s.PlannedLocation is null).Select(s => deliveryOrders[s.OrderId]).ToList();
        var currentLocations = await locations.ResolveAsync(unplanned, cancellationToken);

        return new DeliveryRunDto(
            run.Id,
            run.Name,
            run.PlannedDate,
            run.Status.ToContract(),
            courier is null
                ? new CourierSummaryDto(run.CourierId, string.Empty, string.Empty)
                : new CourierSummaryDto(courier.Id, courier.FullName, courier.PhoneNumber),
            run.VehiclePlate,
            routeLocations[run.StartLocationId].ToDto(),
            routeLocations[run.EndLocationId].ToDto(),
            [.. stops.Select(stop =>
            {
                var order = deliveryOrders[stop.OrderId];
                var location = stop.PlannedLocation ?? currentLocations.GetValueOrDefault(stop.OrderId);
                return new DeliveryStopDto(
                    stop.Id,
                    stop.OrderId,
                    order.Number,
                    stop.DeliverySequence,
                    stop.Status.ToContract(),
                    Enum.Parse<ContractOrderStatus>(order.Status.ToString()),
                    order.RecipientName,
                    order.PhoneNumber,
                    order.Street,
                    order.BuildingNumber,
                    order.ApartmentNumber,
                    order.City,
                    order.PostalCode,
                    location?.Latitude,
                    location?.Longitude,
                    stop.EstimatedArrivalOffsetSeconds,
                    stop.FailureReason,
                    stop.DeliveredAt,
                    order.Notes,
                    [.. order.Items.Select(i => new DeliveryStopItemDto(i.ProductName, i.Quantity))],
                    location is { } point ? navigation.BuildDestinationLink(point) : null);
            })],
            run.ToRoutePlanDto(),
            [.. run.NavigationLinks.Select(l => l.ToDto())],
            run.StartedAt,
            run.CompletedAt,
            run.CreatedAt);
    }
}
