using Kamada.Modules.Ordering.Application.Orders;
using Kamada.Modules.Ordering.Infrastructure.Persistence;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Infrastructure.Seeding;

/// <summary>
/// Places 36 demo orders through <see cref="PlaceOrderHandler"/> (stock reservation, price snapshot and totals as
/// in the real flow) for the seeded clients and products, spread over the last days during shop hours.
/// All orders start as <c>New</c>; payment, packing and delivery seeders move them on.
/// </summary>
internal sealed class OrderingSeeder(OrderingDbContext db, PlaceOrderHandler placeOrder, DemoSeedData seedData, TimeProvider clock) : IDataSeeder
{
    private const int OrderCount = 36;

    private static readonly TimeOnly Opening = new(8, 0);
    private static readonly TimeOnly Closing = new(21, 0);

    private static readonly string[] Notes =
    [
        "Proszę dzwonić domofonem pod numer mieszkania.",
        "Paczkę można zostawić u sąsiada.",
        "Wejście od podwórza.",
        "Proszę o telefon 15 minut przed dostawą.",
        "Brama zamykana – kod 2468.",
        "Po 16:00 proszę dzwonić na komórkę.",
    ];

    public int Order => SeedOrder.Orders;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (seedData.ProductIds.Count == 0 || seedData.ShippingAddresses.Count == 0 || await db.Orders.AnyAsync(cancellationToken))
        {
            return;
        }

        var random = new Random(30);
        var addresses = seedData.ShippingAddresses.OrderBy(_ => random.Next()).ToList();
        var placedAt = PlacementTimes(clock.GetUtcNow(), random);
        var orders = new List<DemoOrder>();

        for (var i = 0; i < OrderCount; i++)
        {
            var address = addresses[i % addresses.Count];
            var lineCount = random.Next(100) switch { < 45 => 1, < 80 => 2, < 95 => 3, _ => 4 };
            var items = seedData.ProductIds
                .OrderBy(_ => random.Next())
                .Take(lineCount)
                .Select(id => new OrderLineRequest(id, random.Next(100) < 75 ? 1 : random.Next(2, 4)))
                .ToList();
            var notes = random.Next(100) < 25 ? Notes[random.Next(Notes.Length)] : null;

            var placed = await placeOrder.HandleAsync(new PlaceOrderRequest(address.ClientId, address.AddressId, items, notes), cancellationToken);
            if (placed.IsFailure)
            {
                throw new InvalidOperationException($"Demo order could not be placed: {placed.Error.Code} – {placed.Error.Description}");
            }

            orders.Add(new DemoOrder(placed.Value.Id, placedAt[i]));
        }

        // created_at is stamped on save; move each order back to the moment it was "placed".
        foreach (var order in orders)
        {
            await db.Orders.Where(o => o.Id == order.OrderId).ExecuteUpdateAsync(s => s.SetProperty(o => o.CreatedAt, order.PlacedAt), cancellationToken);
        }

        // The update changed xmin: later seeders in this scope must load fresh entities.
        db.ChangeTracker.Clear();

        seedData.Orders = orders;
    }

    /// <summary>
    /// Oldest first, 1–3.5 hours apart during shop hours (local time), the newest shortly before now. Placing
    /// them in this order keeps order numbers chronological.
    /// </summary>
    private static List<DateTimeOffset> PlacementTimes(DateTimeOffset now, Random random)
    {
        var times = new List<DateTimeOffset>();
        var time = now.AddMinutes(-random.Next(10, 40));
        while (times.Count < OrderCount)
        {
            var local = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(time, DemoSeedData.TimeZone).DateTime);
            if (local < Opening || local > Closing)
            {
                // Outside shop hours: continue in the evening of the previous (or same, after midnight) day.
                var date = DemoSeedData.LocalDate(time);
                var evening = DemoSeedData.AtLocalTime(local < Opening ? date.AddDays(-1) : date, Closing);
                time = evening.AddMinutes(-random.Next(0, 60));
            }

            times.Add(time);
            time = time.AddMinutes(-random.Next(60, 210));
        }

        times.Reverse();
        return times;
    }
}
