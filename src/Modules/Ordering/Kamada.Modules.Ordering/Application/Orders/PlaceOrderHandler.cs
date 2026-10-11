using Kamada.Modules.Catalog.Contracts;
using Kamada.Modules.Customers.Contracts;
using Kamada.Modules.Ordering.Application.Abstractions;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Ordering.Application.Orders;

/// <summary>
/// Places an order: validates the recipient (Customers), reserves stock and freezes prices (Catalog) and saves the
/// order (Ordering) – all in one database transaction, so stock is never decremented for an order that was not saved.
/// </summary>
internal sealed partial class PlaceOrderHandler(
    IOrderingDbContext db,
    ICustomerDirectory customers,
    ICatalogInventory inventory,
    IWorkerDirectory workers,
    IUnitOfWork unitOfWork,
    ILogger<PlaceOrderHandler> logger)
{
    /// <param name="placedByWorkerId">The signed-in worker (an admin or a seller).</param>
    public async Task<Result<OrderDto>> HandleAsync(PlaceOrderRequest request, Guid placedByWorkerId, CancellationToken cancellationToken)
    {
        var shipping = await customers.GetShippingDetailsAsync(request.ClientId, request.ShippingAddressId, cancellationToken);
        if (shipping.IsFailure)
        {
            return shipping.Error;
        }

        // The same product entered twice becomes one line.
        var lines = request.Items
            .GroupBy(i => i.ProductId)
            .Select(g => new StockLine(g.Key, g.Sum(i => i.Quantity)))
            .ToList();

        var result = await unitOfWork.ExecuteAsync(
            async ct =>
            {
                var reserved = await inventory.ReserveAsync(lines, ct);
                if (reserved.IsFailure)
                {
                    return Result.Failure<Order>(reserved.Error);
                }

                var placed = Order.Place(
                    request.ClientId,
                    ToSnapshot(shipping.Value),
                    [.. reserved.Value.Select(p => new OrderLine(p.ProductId, p.Name, p.Sku, p.Quantity, p.UnitPrice))],
                    request.Notes,
                    placedByWorkerId);
                if (placed.IsFailure)
                {
                    return placed;
                }

                db.Orders.Add(placed.Value);
                await db.SaveChangesAsync(ct);
                return placed;
            },
            cancellationToken);

        if (result.IsFailure)
        {
            return result.Error;
        }

        LogOrderPlaced(result.Value.Number, result.Value.TotalAmount, result.Value.Items.Count);
        return result.Value.ToDto(await workers.GetNamesAsync([placedByWorkerId], cancellationToken));
    }

    private static ShippingAddress ToSnapshot(ShippingDetails details) => new(
        details.AddressId,
        details.ClientName,
        details.PhoneNumber,
        details.Street,
        details.BuildingNumber,
        details.ApartmentNumber,
        details.City,
        details.PostalCode,
        details.Country,
        details.Latitude,
        details.Longitude);

    [LoggerMessage(Level = LogLevel.Information, Message = "Order #{Number} placed: {Total} PLN, {ItemCount} line(s)")]
    private partial void LogOrderPlaced(long number, decimal total, int itemCount);
}
