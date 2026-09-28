using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DeliveryRunStatus = Kamada.Modules.Delivery.Domain.Runs.DeliveryRunStatus;

namespace Kamada.Modules.Delivery.Application.Runs;

/// <summary>
/// Stop order of a run: optimize (preview only), apply the optimized order, or set the order manually.
/// All three evaluate the route on the same travel-time matrix, so their numbers are comparable.
/// </summary>
internal sealed class RoutePlanningHandler(
    IDeliveryDbContext db,
    IOrderFulfillment orders,
    StopLocationResolver locations,
    IRoutePlanner planner,
    INavigationLinkBuilder navigation,
    IOptions<DeliveryOptions> options,
    TimeProvider clock,
    DeliveryRunReader reader)
{
    /// <summary>Proposal and the current order side by side; nothing is saved.</summary>
    public async Task<Result<RouteOptimizationPreviewDto>> PreviewAsync(Guid runId, CancellationToken cancellationToken)
    {
        var run = await db.DeliveryRuns.AsNoTracking().Include(r => r.Stops).SingleOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var context = await LoadContextAsync(run, cancellationToken);
        if (context.IsFailure)
        {
            return context.Error;
        }

        var currentOrder = Enumerable.Range(0, context.Value.Stops.Count).ToList();
        var result = await planner.OptimizeAsync(context.Value.Request, currentOrder, cancellationToken);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return new RouteOptimizationPreviewDto(
            ToDto(context.Value, result.Value.Optimized),
            ToDto(context.Value, result.Value.Current!));
    }

    /// <summary>Optimizes and stores the order, arrival estimates and navigation links.</summary>
    public async Task<Result<DeliveryRunDto>> ApplyOptimizationAsync(Guid runId, CancellationToken cancellationToken) =>
        await PlanAsync(
            runId,
            async (context, ct) =>
            {
                var result = await planner.OptimizeAsync(context.Request, currentOrder: null, ct);
                return result.IsSuccess ? result.Value.Optimized : result.Error;
            },
            cancellationToken);

    /// <summary>Stores a manual order (e.g. after drag and drop) with recomputed times.</summary>
    public async Task<Result<DeliveryRunDto>> SetSequenceAsync(Guid runId, SetRunSequenceRequest request, CancellationToken cancellationToken) =>
        await PlanAsync(
            runId,
            async (context, ct) =>
            {
                var indexByOrder = context.Stops.Select((stop, index) => (stop.OrderId, index)).ToDictionary(x => x.OrderId, x => x.index);
                if (request.OrderIds.Count != indexByOrder.Count || request.OrderIds.Any(id => !indexByOrder.ContainsKey(id)))
                {
                    return DeliveryErrors.SequenceMustContainAllOrders;
                }

                return await planner.EvaluateAsync(context.Request, [.. request.OrderIds.Select(id => indexByOrder[id])], ct);
            },
            cancellationToken);

    private async Task<Result<DeliveryRunDto>> PlanAsync(
        Guid runId,
        Func<RouteContext, CancellationToken, Task<Result<RoutePlan>>> plan,
        CancellationToken cancellationToken)
    {
        var run = await db.DeliveryRuns.Include(r => r.Stops).SingleOrDefaultAsync(r => r.Id == runId, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        // Fail fast before calling the routing provider.
        if (run.Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(run.Status);
        }

        var context = await LoadContextAsync(run, cancellationToken);
        if (context.IsFailure)
        {
            return context.Error;
        }

        var routePlan = await plan(context.Value, cancellationToken);
        if (routePlan.IsFailure)
        {
            return routePlan.Error;
        }

        var dto = ToDto(context.Value, routePlan.Value);
        var route = new PlannedRoute(
            [.. dto.Stops.Select(s => new PlannedStop(s.OrderId, s.ArrivalOffsetSeconds, new GeoPoint(s.Latitude, s.Longitude)))],
            dto.TotalDurationSeconds,
            dto.TotalDistanceMeters,
            dto.Algorithm,
            dto.IsOptimal,
            dto.IsApproximate,
            dto.MatrixProvider,
            [.. dto.NavigationLinks.Select(l => new RunNavigationLink(l.Part, l.TotalParts, l.FromPoint, l.ToPoint, l.Url))]);

        var applied = run.ApplyRoute(route, clock.GetUtcNow());
        if (applied.IsFailure)
        {
            return applied.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await reader.ReadAsync(run, cancellationToken);
    }

    /// <summary>Start, stops in the current sequence and end, all with coordinates – or a readable error.</summary>
    private async Task<Result<RouteContext>> LoadContextAsync(DeliveryRun run, CancellationToken cancellationToken)
    {
        if (run.Stops.Count == 0)
        {
            return DeliveryErrors.RunHasNoStops;
        }

        var endpoints = await db.RouteLocations.AsNoTracking()
            .Where(l => l.Id == run.StartLocationId || l.Id == run.EndLocationId)
            .ToDictionaryAsync(l => l.Id, cancellationToken);
        var start = endpoints[run.StartLocationId];
        var end = endpoints[run.EndLocationId];
        if (start.Location is not { } startPoint)
        {
            return DeliveryErrors.LocationWithoutCoordinates(start.Name);
        }

        if (end.Location is not { } endPoint)
        {
            return DeliveryErrors.LocationWithoutCoordinates(end.Name);
        }

        var deliveryOrders = (await orders.GetDeliveryOrdersAsync([.. run.Stops.Select(s => s.OrderId)], cancellationToken))
            .ToDictionary(o => o.OrderId);
        var points = await locations.ResolveAsync([.. deliveryOrders.Values], cancellationToken);

        var withoutCoordinates = run.Stops
            .Where(s => points.GetValueOrDefault(s.OrderId) is null)
            .Select(s => deliveryOrders[s.OrderId].Number)
            .ToList();
        if (withoutCoordinates.Count > 0)
        {
            return DeliveryErrors.StopsWithoutCoordinates(withoutCoordinates);
        }

        var stops = run.Stops
            .Select(s => new RouteStop(s.OrderId, deliveryOrders[s.OrderId].Number, points[s.OrderId].GetValueOrDefault()))
            .ToList();
        return new RouteContext(new RoutePlanRequest(startPoint, endPoint, [.. stops.Select(s => s.Location)]), stops);
    }

    /// <summary>
    /// Arrival at stop k = driving time of legs 0..k + service time at the k stops before it.
    /// Totals stay pure driving time/distance, as computed by the planner.
    /// </summary>
    private RoutePlanDto ToDto(RouteContext context, RoutePlan plan)
    {
        var serviceSeconds = options.Value.ServiceTimePerStop.TotalSeconds;
        var ordered = plan.StopOrder.Select(i => context.Stops[i]).ToList();

        var stops = new List<PlannedStopDto>(ordered.Count);
        var elapsed = 0d;
        for (var k = 0; k < ordered.Count; k++)
        {
            var leg = plan.Legs[k];
            elapsed += leg.DurationSeconds;
            var stop = ordered[k];
            stops.Add(new PlannedStopDto(stop.OrderId, stop.OrderNumber, k + 1, leg.DurationSeconds, leg.DistanceMeters, elapsed, stop.Location.Latitude, stop.Location.Longitude));
            elapsed += serviceSeconds;
        }

        var returnLeg = plan.Legs[^1];
        var links = navigation.BuildRouteLinks([context.Request.Start, .. ordered.Select(s => s.Location), context.Request.End]);

        return new RoutePlanDto(
            stops,
            plan.TotalDurationSeconds,
            plan.TotalDistanceMeters,
            returnLeg.DurationSeconds,
            returnLeg.DistanceMeters,
            plan.Algorithm,
            plan.IsOptimal,
            plan.IsApproximate,
            plan.MatrixProvider,
            [.. links.Select(l => l.ToRunLink().ToDto())]);
    }

    private sealed record RouteStop(Guid OrderId, long OrderNumber, GeoPoint Location);

    private sealed record RouteContext(RoutePlanRequest Request, IReadOnlyList<RouteStop> Stops);
}
