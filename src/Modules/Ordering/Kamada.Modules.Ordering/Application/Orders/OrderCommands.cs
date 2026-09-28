using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application.Orders;

internal sealed class OrderCommands(IOrderingDbContext db, ICatalogInventory inventory, IUnitOfWork unitOfWork)
{
    /// <summary>Packed and waiting for a delivery run.</summary>
    public async Task<Result> MarkReadyForDeliveryAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        var result = order.MarkReadyForDelivery();
        if (result.IsSuccess)
        {
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    /// <summary>
    /// Cancels the order and returns its items to stock atomically. Refunding a completed payment is a
    /// separate action in the Payments module.
    /// </summary>
    public async Task<Result> CancelAsync(Guid id, CancellationToken cancellationToken)
    {
        var order = await db.Orders.Include(o => o.Items).SingleOrDefaultAsync(o => o.Id == id, cancellationToken);
        if (order is null)
        {
            return OrderErrors.NotFound;
        }

        return await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var cancelled = order.Cancel();
                if (cancelled.IsFailure)
                {
                    return cancelled;
                }

                await db.SaveChangesAsync(ct);
                await inventory.ReleaseAsync([.. order.Items.Select(i => new StockLine(i.ProductId, i.Quantity))], ct);
                return Result.Success();
            },
            cancellationToken);
    }
}
