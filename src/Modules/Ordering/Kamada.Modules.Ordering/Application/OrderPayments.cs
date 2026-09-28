using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application;

/// <summary>Ordering's public API for the Payments module.</summary>
internal sealed class OrderPayments(IOrderingDbContext db) : IOrderPayments
{
    public async Task<Result<OrderForPayment>> GetOrderForPaymentAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking()
            .Where(o => o.Id == orderId)
            .Select(o => new { o.Id, o.Number, o.TotalAmount, o.Status })
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var state = order.Status switch
        {
            OrderStatus.New => OrderPaymentState.AwaitingPayment,
            OrderStatus.Cancelled => OrderPaymentState.Cancelled,
            _ => OrderPaymentState.Paid,
        };
        return new OrderForPayment(order.Id, order.Number, order.TotalAmount, state);
    }

    public async Task<Result> MarkPaidAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == orderId, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var result = order.MarkPaid();
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }
}
