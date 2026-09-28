using Kamada.Modules.Delivery.Application.Runs;
using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Modules.Delivery.Infrastructure.Persistence;
using Kamada.Modules.Ordering.Contracts;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Geography;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Delivery.Infrastructure.Seeding;

/// <summary>
/// Route locations (warehouse and courier base, coordinates stored here) and two runs of courier <c>kurier1</c>,
/// created through the real use cases (create → optimize → approve → courier actions):
/// yesterday's completed run – replayed with a <see cref="SeedClock"/>, one stop failed – and today's approved run
/// that has not started yet. The remaining ready orders are left for planning in the admin panel.
/// </summary>
internal sealed class DeliverySeeder(
    DeliveryDbContext db,
    DeliveryRunCommands runs,
    IOrderFulfillment orders,
    IServiceProvider services,
    DemoSeedData seedData,
    TimeProvider clock) : IDataSeeder
{
    private const int CompletedRunStops = 7;
    private const int TodayRunStops = 8;
    private const int FailedStopIndex = 3;

    public int Order => SeedOrder.Delivery;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.RouteLocations.AnyAsync(cancellationToken) || await db.DeliveryRuns.AnyAsync(cancellationToken))
        {
            return;
        }

        var warehouse = Location("Magazyn centralny", "Przemysłowa", "8", "85-756", 53.1225509, 18.0999879);
        var courierBase = Location("Baza kurierów Kapuściska", "Bydgoskich Przemysłowców", "6", "85-862", 53.0841856, 18.0676411);
        db.RouteLocations.AddRange(warehouse, courierBase);
        await db.SaveChangesAsync(cancellationToken);

        if (seedData.CourierIds.Count == 0 || seedData.Orders.Count == 0)
        {
            return;
        }

        var courierId = seedData.CourierIds[0];
        var now = clock.GetUtcNow();
        var today = DemoSeedData.LocalDate(now);
        var yesterday = today.AddDays(-1);

        var placedAt = seedData.Orders.ToDictionary(o => o.OrderId, o => o.PlacedAt);
        var ready = (await orders.GetDeliveryOrdersAsync(placedAt.Keys, cancellationToken))
            .Where(o => o.Status == FulfillmentStatus.ReadyForDelivery)
            .OrderBy(o => placedAt[o.OrderId])
            .Select(o => o.OrderId)
            .ToList();

        var morning = DemoSeedData.AtLocalTime(yesterday, new TimeOnly(7, 0));
        var completedOrders = ready.Where(id => placedAt[id] < morning).Take(CompletedRunStops).ToList();
        var todayOrders = ready.Except(completedOrders).Take(TodayRunStops).ToList();

        if (completedOrders.Count > 0)
        {
            var request = new CreateDeliveryRunRequest("Trasa poranna", yesterday, courierId, "CB 4521K", warehouse.Id, warehouse.Id, completedOrders);
            await ReplayCompletedRunAsync(request, DemoSeedData.AtLocalTime(yesterday, new TimeOnly(8, 30)), cancellationToken);
        }

        if (todayOrders.Count > 0)
        {
            var request = new CreateDeliveryRunRequest("Trasa dzienna", today, courierId, "CB 4521K", warehouse.Id, courierBase.Id, todayOrders);
            var run = Ensure(await runs.CreateAsync(request, cancellationToken));
            var planning = services.GetRequiredService<RoutePlanningHandler>();
            Ensure(await planning.ApplyOptimizationAsync(run.Id, cancellationToken));
            Ensure(await runs.ApproveAsync(run.Id, cancellationToken));
        }
    }

    /// <summary>Planned the evening before, driven from <paramref name="startAt"/> with small delays at each stop.</summary>
    private async Task ReplayCompletedRunAsync(CreateDeliveryRunRequest request, DateTimeOffset startAt, CancellationToken cancellationToken)
    {
        var seedClock = new SeedClock(startAt.AddHours(-12));
        var planning = ActivatorUtilities.CreateInstance<RoutePlanningHandler>(services, seedClock);
        var courier = ActivatorUtilities.CreateInstance<CourierRunCommands>(services, seedClock);
        var random = new Random(50);

        var run = Ensure(await runs.CreateAsync(request, cancellationToken));
        run = Ensure(await planning.ApplyOptimizationAsync(run.Id, cancellationToken));
        Ensure(await runs.ApproveAsync(run.Id, cancellationToken));

        seedClock.Now = startAt;
        Ensure(await courier.StartAsync(run.Id, request.CourierId, cancellationToken));

        var delay = TimeSpan.Zero;
        var stops = run.Stops.OrderBy(s => s.Sequence).ToList();
        for (var i = 0; i < stops.Count; i++)
        {
            var stop = stops[i];
            delay += TimeSpan.FromMinutes(random.Next(0, 5));
            var offset = TimeSpan.FromSeconds(stop.EstimatedArrivalOffsetSeconds ?? (i + 1) * 900);
            seedClock.Now = startAt + offset + delay;

            // The last stop completes the run automatically.
            Ensure(i == FailedStopIndex
                ? await courier.MarkFailedAsync(run.Id, stop.Id, new FailStopRequest("Odbiorca nie odbiera telefonu, brak kontaktu."), request.CourierId, cancellationToken)
                : await courier.MarkDeliveredAsync(run.Id, stop.Id, request.CourierId, cancellationToken));
        }
    }

    private static RouteLocation Location(string name, string street, string buildingNumber, string postalCode, double latitude, double longitude)
    {
        var location = RouteLocation.Create(new RouteLocationDetails(name, street, buildingNumber, "Bydgoszcz", postalCode, "Polska"));
        location.SetLocation(new GeoPoint(latitude, longitude));
        return location;
    }

    private static T Ensure<T>(Result<T> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Demo delivery run failed: {result.Error.Code} – {result.Error.Description}");
}
