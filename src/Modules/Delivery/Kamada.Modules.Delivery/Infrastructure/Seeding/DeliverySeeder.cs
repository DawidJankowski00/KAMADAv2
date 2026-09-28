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
/// Route locations (a warehouse and a courier base per delivery area, coordinates stored here) and runs created
/// through the real use cases (create → optimize → approve → courier actions):
/// in the Tricity courier <c>kurier1</c> has yesterday's completed run – replayed with a <see cref="SeedClock"/>,
/// one stop failed – and today's approved run; in Szczecin courier <c>kurier2</c> has today's approved run.
/// The remaining ready orders are left for planning in the admin panel.
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
    private const int TodayRunStops = 7;
    private const int FailedStopIndex = 3;

    public int Order => SeedOrder.Delivery;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.RouteLocations.AnyAsync(cancellationToken) || await db.DeliveryRuns.AnyAsync(cancellationToken))
        {
            return;
        }

        var tricityWarehouse = Location("Magazyn Gdańsk", "Budowlanych", "64D", "Gdańsk", "80-298", 54.3696235, 18.5065513);
        var tricityBase = Location("Baza kurierów Gdynia", "Hutnicza", "40", "Gdynia", "81-012", 54.5535249, 18.4501734);
        var szczecinWarehouse = Location("Magazyn Szczecin", "Pomorska", "115b", "Szczecin", "70-812", 53.3870310, 14.6874501);
        var szczecinBase = Location("Baza kurierów Szczecin", "Gryfińska", "15", "Szczecin", "70-806", 53.3919227, 14.6612222);
        db.RouteLocations.AddRange(tricityWarehouse, tricityBase, szczecinWarehouse, szczecinBase);
        await db.SaveChangesAsync(cancellationToken);

        if (seedData.CourierIds.Count < 2 || seedData.Orders.Count == 0)
        {
            return;
        }

        var now = clock.GetUtcNow();
        var today = DemoSeedData.LocalDate(now);
        var yesterday = today.AddDays(-1);

        var placed = seedData.Orders.ToDictionary(o => o.OrderId);
        var ready = (await orders.GetDeliveryOrdersAsync(placed.Keys, cancellationToken))
            .Where(o => o.Status == FulfillmentStatus.ReadyForDelivery)
            .Select(o => placed[o.OrderId])
            .OrderBy(o => o.PlacedAt)
            .ToList();

        var morning = DemoSeedData.AtLocalTime(yesterday, new TimeOnly(7, 0));
        var tricity = ready.Where(o => o.Region == DemoRegion.Tricity).ToList();
        var completedOrders = tricity.Where(o => o.PlacedAt < morning).Take(CompletedRunStops).Select(o => o.OrderId).ToList();
        var tricityToday = tricity.Select(o => o.OrderId).Except(completedOrders).Take(TodayRunStops).ToList();
        var szczecinToday = ready.Where(o => o.Region == DemoRegion.Szczecin).Take(TodayRunStops).Select(o => o.OrderId).ToList();

        var (tricityCourier, szczecinCourier) = (seedData.CourierIds[0], seedData.CourierIds[1]);
        if (completedOrders.Count > 0)
        {
            var request = new CreateDeliveryRunRequest("Trójmiasto – poranna", yesterday, tricityCourier, "GD 4521K", tricityWarehouse.Id, tricityWarehouse.Id, completedOrders);
            await ReplayCompletedRunAsync(request, DemoSeedData.AtLocalTime(yesterday, new TimeOnly(8, 30)), cancellationToken);
        }

        await PlanApprovedRunAsync(new CreateDeliveryRunRequest("Trójmiasto – dzienna", today, tricityCourier, "GD 4521K", tricityWarehouse.Id, tricityBase.Id, tricityToday), cancellationToken);
        await PlanApprovedRunAsync(new CreateDeliveryRunRequest("Szczecin – dzienna", today, szczecinCourier, "ZS 7310M", szczecinWarehouse.Id, szczecinBase.Id, szczecinToday), cancellationToken);
    }

    private async Task PlanApprovedRunAsync(CreateDeliveryRunRequest request, CancellationToken cancellationToken)
    {
        if (request.OrderIds is not { Count: > 0 })
        {
            return;
        }

        var run = Ensure(await runs.CreateAsync(request, cancellationToken));
        var planning = services.GetRequiredService<RoutePlanningHandler>();
        Ensure(await planning.ApplyOptimizationAsync(run.Id, cancellationToken));
        Ensure(await runs.ApproveAsync(run.Id, cancellationToken));
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

    private static RouteLocation Location(string name, string street, string buildingNumber, string city, string postalCode, double latitude, double longitude)
    {
        var location = RouteLocation.Create(new RouteLocationDetails(name, street, buildingNumber, city, postalCode, "Polska"));
        location.SetLocation(new GeoPoint(latitude, longitude));
        return location;
    }

    private static T Ensure<T>(Result<T> result) =>
        result.IsSuccess
            ? result.Value
            : throw new InvalidOperationException($"Demo delivery run failed: {result.Error.Code} – {result.Error.Description}");
}
