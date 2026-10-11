using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application.Orders;

internal sealed class OrderQueries(IOrderingDbContext db, IWorkerDirectory workers)
{
    public async Task<PagedResult<OrderSummaryDto>> ListAsync(OrderListQuery query, CancellationToken cancellationToken)
    {
        var orders = db.Orders.AsNoTracking();

        if (query.Status is { } status)
        {
            var domainStatus = status.ToDomain();
            orders = orders.Where(o => o.Status == domainStatus);
        }

        if (query.ClientId is { } clientId)
        {
            orders = orders.Where(o => o.ClientId == clientId);
        }

        if (query.PlacedBy is { } placedBy)
        {
            orders = orders.Where(o => o.PlacedByWorkerId == placedBy);
        }

        if (query.From is { } from)
        {
            var fromUtc = from.ToUniversalTime();
            orders = orders.Where(o => o.CreatedAt >= fromUtc);
        }

        if (query.To is { } to)
        {
            var toUtc = to.ToUniversalTime();
            orders = orders.Where(o => o.CreatedAt < toUtc);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim().TrimStart('#');
            if (long.TryParse(search, out var number))
            {
                orders = orders.Where(o => o.Number == number);
            }
            else
            {
                var pattern = LikePattern.Contains(search);
                orders = orders.Where(o => EF.Functions.ILike(o.ShippingAddress.RecipientName, pattern));
            }
        }

        var total = await orders.CountAsync(cancellationToken);
        var rows = await orders
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Number)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(o => new
            {
                o.Id,
                o.Number,
                o.ClientId,
                o.PlacedByWorkerId,
                o.ShippingAddress.RecipientName,
                o.ShippingAddress.City,
                o.Status,
                o.TotalAmount,
                ItemCount = o.Items.Count,
                o.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        var names = await workers.GetNamesAsync([.. rows.Where(r => r.PlacedByWorkerId is not null).Select(r => r.PlacedByWorkerId!.Value)], cancellationToken);
        var items = rows
            .Select(r => new OrderSummaryDto(r.Id, r.Number, r.ClientId, OrderMappings.PlacedBy(r.PlacedByWorkerId, names), r.RecipientName, r.City, r.Status.ToContract(), r.TotalAmount, r.ItemCount, r.CreatedAt))
            .ToList();
        return new PagedResult<OrderSummaryDto>(items, query.Page, query.PageSize, total);
    }

    /// <param name="placedBy">When set, an order placed by someone else is reported as not found (a seller sees only their own).</param>
    public async Task<Result<OrderDto>> GetAsync(Guid id, Guid? placedBy, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null || (placedBy is not null && order.PlacedByWorkerId != placedBy))
        {
            return OrderErrors.NotFound;
        }

        var names = order.PlacedByWorkerId is { } workerId ? await workers.GetNamesAsync([workerId], cancellationToken) : new Dictionary<Guid, string>();
        return order.ToDto(names);
    }

    /// <summary>Order counts per status (dashboard). Statuses without orders are reported as 0.</summary>
    public async Task<IReadOnlyList<OrderStatusCountDto>> CountByStatusAsync(CancellationToken cancellationToken)
    {
        var counts = await db.Orders.AsNoTracking()
            .GroupBy(o => o.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        return
        [
            .. Enum.GetValues<Domain.Orders.OrderStatus>()
                .Select(s => new OrderStatusCountDto(s.ToContract(), counts.GetValueOrDefault(s))),
        ];
    }
}
