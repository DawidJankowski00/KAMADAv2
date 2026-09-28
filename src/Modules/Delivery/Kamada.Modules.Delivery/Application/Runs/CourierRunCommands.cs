using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Delivery.Application.Runs;

/// <summary>
/// What a courier does on the road. A courier can act only on their own runs – others are "not found".
/// Each stop outcome updates the order in the same transaction.
/// </summary>
internal sealed class CourierRunCommands(
    IDeliveryDbContext db,
    IOrderFulfillment orders,
    IUnitOfWork unitOfWork,
    TimeProvider clock,
    DeliveryRunReader reader)
{
    /// <summary><c>Planned → InProgress</c>; all orders go out for delivery.</summary>
    public Task<Result<DeliveryRunDto>> StartAsync(Guid runId, Guid courierId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            runId,
            courierId,
            run =>
            {
                var started = run.Start(clock.GetUtcNow());
                return started.IsSuccess
                    ? Result.Success<IReadOnlyCollection<Guid>>([.. run.Stops.Select(s => s.OrderId)])
                    : started.Error;
            },
            DeliveryTransition.OutForDelivery,
            cancellationToken);

    public Task<Result<DeliveryRunDto>> MarkDeliveredAsync(Guid runId, Guid stopId, Guid courierId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            runId,
            courierId,
            run =>
            {
                var stop = run.MarkStopDelivered(stopId, clock.GetUtcNow());
                return stop.IsSuccess ? Result.Success<IReadOnlyCollection<Guid>>([stop.Value.OrderId]) : stop.Error;
            },
            DeliveryTransition.Delivered,
            cancellationToken);

    public Task<Result<DeliveryRunDto>> MarkFailedAsync(Guid runId, Guid stopId, FailStopRequest request, Guid courierId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            runId,
            courierId,
            run =>
            {
                var stop = run.MarkStopFailed(stopId, request.Reason, clock.GetUtcNow());
                return stop.IsSuccess ? Result.Success<IReadOnlyCollection<Guid>>([stop.Value.OrderId]) : stop.Error;
            },
            DeliveryTransition.DeliveryFailed,
            cancellationToken);

    /// <summary>Finishes the run early; stops not visited are recorded as failed.</summary>
    public Task<Result<DeliveryRunDto>> CompleteAsync(Guid runId, Guid courierId, CancellationToken cancellationToken) =>
        ExecuteAsync(
            runId,
            courierId,
            run =>
            {
                var notVisited = run.Complete(clock.GetUtcNow());
                return notVisited.IsSuccess ? Result.Success<IReadOnlyCollection<Guid>>([.. notVisited.Value]) : notVisited.Error;
            },
            DeliveryTransition.DeliveryFailed,
            cancellationToken);

    /// <param name="change">Domain change; returns the orders to move with <paramref name="transition"/>.</param>
    private async Task<Result<DeliveryRunDto>> ExecuteAsync(
        Guid runId,
        Guid courierId,
        Func<DeliveryRun, Result<IReadOnlyCollection<Guid>>> change,
        DeliveryTransition transition,
        CancellationToken cancellationToken)
    {
        var run = await db.DeliveryRuns
            .Include(r => r.Stops)
            .SingleOrDefaultAsync(DeliveryRunQueries.VisibleToCourier(runId, courierId), cancellationToken);
        if (run is null)
        {
            return DeliveryErrors.RunNotFound;
        }

        var saved = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var affected = change(run);
                if (affected.IsFailure)
                {
                    return Result.Failure(affected.Error);
                }

                await db.SaveChangesAsync(ct);
                return await orders.ApplyAsync(affected.Value, transition, ct);
            },
            cancellationToken);

        return saved.IsSuccess ? await reader.ReadAsync(run, cancellationToken) : saved.Error;
    }
}
