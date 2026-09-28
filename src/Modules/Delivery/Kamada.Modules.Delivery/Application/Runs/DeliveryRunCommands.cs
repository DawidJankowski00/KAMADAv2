using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using DeliveryRunStatus = Kamada.Modules.Delivery.Domain.Runs.DeliveryRunStatus;

namespace Kamada.Modules.Delivery.Application.Runs;

/// <summary>
/// Admin use cases of delivery runs. Every change of which orders belong to a run also moves those orders
/// through their lifecycle (Ordering) in the same transaction.
/// </summary>
internal sealed class DeliveryRunCommands(
    IDeliveryDbContext db,
    IOrderFulfillment orders,
    ICourierDirectory couriers,
    IUnitOfWork unitOfWork,
    DeliveryRunReader reader)
{
    public async Task<Result<DeliveryRunDto>> CreateAsync(CreateDeliveryRunRequest request, CancellationToken cancellationToken)
    {
        var details = new RunDetails(request.Name, request.PlannedDate, request.CourierId, request.VehiclePlate, request.StartLocationId, request.EndLocationId);
        var valid = await ValidateDetailsAsync(details, previous: null, cancellationToken);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var run = DeliveryRun.Create(details);
        var orderIds = request.OrderIds ?? [];
        var ready = await CheckOrdersReadyAsync(orderIds, cancellationToken);
        if (ready.IsFailure)
        {
            return ready.Error;
        }

        var result = run.AddOrders(orderIds);
        if (result.IsFailure)
        {
            return result.Error;
        }

        db.DeliveryRuns.Add(run);
        return await SaveWithOrdersAsync(run, orderIds, DeliveryTransition.AssignToRun, cancellationToken);
    }

    public async Task<Result<DeliveryRunDto>> UpdateAsync(Guid id, UpdateDeliveryRunRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(id, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var details = new RunDetails(request.Name, request.PlannedDate, request.CourierId, request.VehiclePlate, request.StartLocationId, request.EndLocationId);
        var valid = await ValidateDetailsAsync(details, run, cancellationToken);
        if (valid.IsFailure)
        {
            return valid.Error;
        }

        var result = run.UpdateDetails(details);
        if (result.IsFailure)
        {
            return result.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await reader.ReadAsync(run, cancellationToken);
    }

    public async Task<Result<DeliveryRunDto>> AddOrdersAsync(Guid id, AddRunOrdersRequest request, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(id, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        if (run.Status != DeliveryRunStatus.Draft)
        {
            return DeliveryErrors.RunNotEditable(run.Status);
        }

        if (request.OrderIds.Any(orderId => run.Stops.Any(s => s.OrderId == orderId)))
        {
            return DeliveryErrors.OrderAlreadyInThisRun;
        }

        var ready = await CheckOrdersReadyAsync(request.OrderIds, cancellationToken);
        if (ready.IsFailure)
        {
            return ready.Error;
        }

        var result = run.AddOrders(request.OrderIds);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await SaveWithOrdersAsync(run, request.OrderIds, DeliveryTransition.AssignToRun, cancellationToken);
    }

    public async Task<Result<DeliveryRunDto>> RemoveOrderAsync(Guid id, Guid orderId, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(id, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var result = run.RemoveOrder(orderId);
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await SaveWithOrdersAsync(run, [orderId], DeliveryTransition.RemoveFromRun, cancellationToken);
    }

    public async Task<Result<DeliveryRunDto>> ApproveAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(id, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var result = run.Approve();
        if (result.IsFailure)
        {
            return result.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return await reader.ReadAsync(run, cancellationToken);
    }

    /// <summary>Cancels a run that has not started; its orders become ready for delivery again.</summary>
    public async Task<Result<DeliveryRunDto>> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(id, cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var result = run.Cancel();
        if (result.IsFailure)
        {
            return result.Error;
        }

        return await SaveWithOrdersAsync(run, [.. run.Stops.Select(s => s.OrderId)], DeliveryTransition.RemoveFromRun, cancellationToken);
    }

    private Task<DeliveryRun?> LoadAsync(Guid id, CancellationToken cancellationToken) =>
        db.DeliveryRuns.Include(r => r.Stops).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    private async Task<Result<DeliveryRunDto>> SaveWithOrdersAsync(
        DeliveryRun run,
        IReadOnlyCollection<Guid> orderIds,
        DeliveryTransition transition,
        CancellationToken cancellationToken)
    {
        var saved = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                await db.SaveChangesAsync(ct);
                return await orders.ApplyAsync(orderIds, transition, ct);
            },
            cancellationToken);

        return saved.IsSuccess ? await reader.ReadAsync(run, cancellationToken) : saved.Error;
    }

    /// <summary>Orders must exist and wait for delivery (not paid yet, cancelled or in another run → rejected).</summary>
    private async Task<Result> CheckOrdersReadyAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return Result.Success();
        }

        var found = await orders.GetDeliveryOrdersAsync(orderIds, cancellationToken);
        var missing = orderIds.Except(found.Select(o => o.OrderId)).ToList();
        if (missing.Count > 0)
        {
            return DeliveryErrors.OrdersNotFound(missing);
        }

        var notReady = found.Where(o => o.Status != FulfillmentStatus.ReadyForDelivery).Select(o => o.Number).ToList();
        return notReady.Count > 0 ? DeliveryErrors.OrdersNotReady(notReady) : Result.Success();
    }

    /// <summary>Courier must be an active courier; locations must exist and be active (unchanged ones may stay inactive).</summary>
    private async Task<Result> ValidateDetailsAsync(RunDetails details, DeliveryRun? previous, CancellationToken cancellationToken)
    {
        if (previous is null || previous.CourierId != details.CourierId)
        {
            var courier = await couriers.GetAssignableCourierAsync(details.CourierId, cancellationToken);
            if (courier.IsFailure)
            {
                return courier.Error;
            }
        }

        var locationIds = new[] { details.StartLocationId, details.EndLocationId }.Distinct().ToList();
        var locations = await db.RouteLocations.AsNoTracking().Where(l => locationIds.Contains(l.Id)).ToListAsync(cancellationToken);
        if (locations.Count != locationIds.Count)
        {
            return Error.Validation(DeliveryErrors.LocationNotFound.Code, DeliveryErrors.LocationNotFound.Description);
        }

        var unchanged = previous is null ? [] : new[] { previous.StartLocationId, previous.EndLocationId };
        return locations.Any(l => !l.IsActive && !unchanged.Contains(l.Id)) ? DeliveryErrors.LocationInactive : Result.Success();
    }
}
