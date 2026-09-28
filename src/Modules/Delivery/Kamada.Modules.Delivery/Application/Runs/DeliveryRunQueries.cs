using Kamada.Modules.Delivery.Application.Abstractions;
using Kamada.Modules.Delivery.Domain;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DeliveryRunStatus = Kamada.Modules.Delivery.Domain.Runs.DeliveryRunStatus;
using DeliveryStopStatus = Kamada.Modules.Delivery.Domain.Runs.DeliveryStopStatus;

namespace Kamada.Modules.Delivery.Application.Runs;

internal sealed class DeliveryRunQueries(
    IDeliveryDbContext db,
    DeliveryRunReader reader,
    ICourierDirectory couriers,
    IOptions<DeliveryOptions> options,
    TimeProvider clock)
{
    /// <summary>How many completed runs the courier app lists.</summary>
    public const int CompletedRunsShown = 20;

    public async Task<PagedResult<DeliveryRunSummaryDto>> ListAsync(DeliveryRunListQuery query, CancellationToken cancellationToken)
    {
        var runs = db.DeliveryRuns.AsNoTracking();

        if (query.Status is { } status)
        {
            var domainStatus = status.ToDomain();
            runs = runs.Where(r => r.Status == domainStatus);
        }

        if (query.CourierId is { } courierId)
        {
            runs = runs.Where(r => r.CourierId == courierId);
        }

        if (query.From is { } from)
        {
            runs = runs.Where(r => r.PlannedDate >= from);
        }

        if (query.To is { } to)
        {
            runs = runs.Where(r => r.PlannedDate <= to);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            runs = runs.Where(r => EF.Functions.ILike(r.Name, pattern) || EF.Functions.ILike(r.VehiclePlate, pattern));
        }

        var total = await runs.CountAsync(cancellationToken);
        var page = runs
            .OrderByDescending(r => r.PlannedDate).ThenByDescending(r => r.CreatedAt)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize);

        return new PagedResult<DeliveryRunSummaryDto>(await SummarizeAsync(page, cancellationToken), query.Page, query.PageSize, total);
    }

    public async Task<Result<DeliveryRunDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(db.DeliveryRuns.Where(r => r.Id == id), cancellationToken);
        return run is null ? DeliveryErrors.RunNotFound : await reader.ReadAsync(run, cancellationToken);
    }

    /// <summary>Runs of the signed-in courier; drafts and cancelled runs are not shown to couriers.</summary>
    public async Task<IReadOnlyList<DeliveryRunSummaryDto>> ListForCourierAsync(Guid courierId, CourierRunScope scope, CancellationToken cancellationToken)
    {
        var today = Today();
        var runs = db.DeliveryRuns.AsNoTracking().Where(r => r.CourierId == courierId);

        runs = scope switch
        {
            CourierRunScope.Current => runs
                .Where(r => r.Status == DeliveryRunStatus.InProgress || (r.Status == DeliveryRunStatus.Planned && r.PlannedDate <= today))
                .OrderBy(r => r.PlannedDate).ThenBy(r => r.CreatedAt),
            CourierRunScope.Upcoming => runs
                .Where(r => r.Status == DeliveryRunStatus.Planned && r.PlannedDate > today)
                .OrderBy(r => r.PlannedDate).ThenBy(r => r.CreatedAt),
            CourierRunScope.Completed => runs
                .Where(r => r.Status == DeliveryRunStatus.Completed)
                .OrderByDescending(r => r.CompletedAt)
                .Take(CompletedRunsShown),
            _ => throw new ArgumentOutOfRangeException(nameof(scope), scope, null),
        };

        return await SummarizeAsync(runs, cancellationToken);
    }

    /// <summary>Another courier's run is reported as not found, so run ids cannot be probed.</summary>
    public async Task<Result<DeliveryRunDto>> GetForCourierAsync(Guid id, Guid courierId, CancellationToken cancellationToken)
    {
        var run = await LoadAsync(db.DeliveryRuns.Where(VisibleToCourier(id, courierId)), cancellationToken);
        return run is null ? DeliveryErrors.RunNotFound : await reader.ReadAsync(run, cancellationToken);
    }

    public static System.Linq.Expressions.Expression<Func<DeliveryRun, bool>> VisibleToCourier(Guid id, Guid courierId) =>
        r => r.Id == id
            && r.CourierId == courierId
            && (r.Status == DeliveryRunStatus.Planned || r.Status == DeliveryRunStatus.InProgress || r.Status == DeliveryRunStatus.Completed);

    private static Task<DeliveryRun?> LoadAsync(IQueryable<DeliveryRun> runs, CancellationToken cancellationToken) =>
        runs.AsNoTracking().Include(r => r.Stops).SingleOrDefaultAsync(cancellationToken);

    private DateOnly Today()
    {
        var zone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZone);
        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(clock.GetUtcNow(), zone).DateTime);
    }

    private async Task<IReadOnlyList<DeliveryRunSummaryDto>> SummarizeAsync(IQueryable<DeliveryRun> runs, CancellationToken cancellationToken)
    {
        var rows = await runs
            .Select(r => new
            {
                Run = r,
                StopCount = db.DeliveryRunOrders.Count(s => s.DeliveryRunId == r.Id),
                DeliveredCount = db.DeliveryRunOrders.Count(s => s.DeliveryRunId == r.Id && s.Status == DeliveryStopStatus.Delivered),
                FailedCount = db.DeliveryRunOrders.Count(s => s.DeliveryRunId == r.Id && s.Status == DeliveryStopStatus.Failed),
            })
            .ToListAsync(cancellationToken);

        var locationIds = rows.SelectMany(r => new[] { r.Run.StartLocationId, r.Run.EndLocationId }).Distinct().ToList();
        var locationNames = await db.RouteLocations.AsNoTracking()
            .Where(l => locationIds.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, cancellationToken);
        var courierNames = await couriers.GetCouriersAsync([.. rows.Select(r => r.Run.CourierId)], cancellationToken);

        return [.. rows.Select(row => new DeliveryRunSummaryDto(
            row.Run.Id,
            row.Run.Name,
            row.Run.PlannedDate,
            row.Run.Status.ToContract(),
            row.Run.CourierId,
            courierNames.TryGetValue(row.Run.CourierId, out var courier) ? courier.FullName : string.Empty,
            row.Run.VehiclePlate,
            locationNames.GetValueOrDefault(row.Run.StartLocationId, string.Empty),
            locationNames.GetValueOrDefault(row.Run.EndLocationId, string.Empty),
            row.StopCount,
            row.DeliveredCount,
            row.FailedCount,
            row.Run.OptimizedDurationSeconds,
            row.Run.OptimizedDistanceMeters))];
    }
}
