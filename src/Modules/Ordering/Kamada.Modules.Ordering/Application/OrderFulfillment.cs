using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application;

/// <summary>Ordering's public API for the Delivery module.</summary>
internal sealed class OrderFulfillment(IOrderingDbContext db) : IOrderFulfillment
{
    public async Task<IReadOnlyList<DeliveryOrder>> GetDeliveryOrdersAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken cancellationToken)
    {
        var ids = orderIds.Distinct().ToList();
        var orders = await db.Orders.AsNoTracking()
            .Include(o => o.Items)
            .Where(o => ids.Contains(o.Id))
            .ToListAsync(cancellationToken);

        return [.. orders.Select(ToDeliveryOrder)];
    }

    public async Task<Result> ApplyAsync(IReadOnlyCollection<Guid> orderIds, DeliveryTransition transition, CancellationToken cancellationToken)
    {
        var ids = orderIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Result.Success();
        }

        var orders = await db.Orders.Where(o => ids.Contains(o.Id)).ToListAsync(cancellationToken);
        if (orders.Count != ids.Count)
        {
            return OrderErrors.NotFound;
        }

        foreach (var order in orders)
        {
            var result = transition switch
            {
                DeliveryTransition.AssignToRun => order.AssignToRun(),
                DeliveryTransition.RemoveFromRun => order.ReturnToReadyForDelivery(),
                DeliveryTransition.OutForDelivery => order.MarkOutForDelivery(),
                DeliveryTransition.Delivered => order.MarkDelivered(),
                DeliveryTransition.DeliveryFailed => order.MarkDeliveryFailed(),
                _ => throw new ArgumentOutOfRangeException(nameof(transition), transition, null),
            };

            if (result.IsFailure)
            {
                // Tracked changes are discarded with the scope; the caller's transaction is rolled back.
                return result;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private static DeliveryOrder ToDeliveryOrder(Order order)
    {
        var address = order.ShippingAddress;
        return new DeliveryOrder(
            order.Id,
            order.Number,
            Enum.Parse<FulfillmentStatus>(order.Status.ToString()),
            address.AddressId,
            address.RecipientName,
            address.PhoneNumber,
            address.Street,
            address.BuildingNumber,
            address.ApartmentNumber,
            address.City,
            address.PostalCode,
            address.Latitude,
            address.Longitude,
            order.Notes,
            order.TotalAmount,
            [.. order.Items.Select(i => new DeliveryOrderLine(i.ProductName, i.Quantity))]);
    }
}
