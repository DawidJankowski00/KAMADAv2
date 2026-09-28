using Kamada.Modules.Ordering.Application.Orders;
using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Modules.Ordering.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Infrastructure.Seeding;

/// <summary>
/// Runs after the payment seeder, through <see cref="OrderCommands"/>: orders left unpaid for two days are
/// cancelled (stock returned), one paid order is cancelled by the customer (its payment can be refunded in the
/// admin panel), orders paid in the last hours are still being packed and the rest are ready for delivery.
/// </summary>
internal sealed class OrderPackingSeeder(OrderingDbContext db, OrderCommands commands, DemoSeedData seedData, TimeProvider clock) : IDataSeeder
{
    private static readonly TimeSpan UnpaidCancellationAfter = TimeSpan.FromDays(2);
    private static readonly TimeSpan PackingTime = TimeSpan.FromHours(6);

    public int Order => SeedOrder.Packing;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (seedData.Orders.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var ids = seedData.Orders.Select(o => o.OrderId).ToList();
        var statuses = await db.Orders.AsNoTracking()
            .Where(o => ids.Contains(o.Id))
            .ToDictionaryAsync(o => o.Id, o => o.Status, cancellationToken);

        // The customer changed their mind about a paid order from a few days ago.
        var cancelledByCustomer = seedData.Orders
            .Where(o => statuses[o.OrderId] == OrderStatus.Paid && now - o.PlacedAt > TimeSpan.FromDays(3))
            .Select(o => o.OrderId)
            .LastOrDefault();

        foreach (var order in seedData.Orders)
        {
            var status = statuses[order.OrderId];
            var age = now - order.PlacedAt;
            var result = status switch
            {
                OrderStatus.New when age > UnpaidCancellationAfter => await commands.CancelAsync(order.OrderId, cancellationToken),
                OrderStatus.Paid when order.OrderId == cancelledByCustomer => await commands.CancelAsync(order.OrderId, cancellationToken),
                OrderStatus.Paid when age > PackingTime => await commands.MarkReadyForDeliveryAsync(order.OrderId, cancellationToken),
                _ => null,
            };

            if (result is { IsFailure: true })
            {
                throw new InvalidOperationException($"Demo order could not be updated: {result.Error.Code} – {result.Error.Description}");
            }
        }
    }
}
