using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Shared.Kernel.Geography;

namespace Kamada.Modules.Delivery.UnitTests;

public sealed class DeliveryRunTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 8, 0, 0, TimeSpan.Zero);
    private static readonly Guid Warehouse = Guid.CreateVersion7();

    private static RunDetails Details(Guid? endLocationId = null) =>
        new("  Poranna Bydgoszcz ", new DateOnly(2026, 9, 29), Guid.CreateVersion7(), " cb  12345 ", Warehouse, endLocationId ?? Warehouse);

    private static DeliveryRun DraftWith(params Guid[] orderIds)
    {
        var run = DeliveryRun.Create(Details());
        run.AddOrders(orderIds).IsSuccess.ShouldBeTrue();
        return run;
    }

    private static PlannedRoute Route(params Guid[] orderIds) => new(
        [.. orderIds.Select((id, i) => new PlannedStop(id, 600 * (i + 1), new GeoPoint(53.12 + i / 100d, 18.0)))],
        DurationSeconds: 3600,
        DistanceMeters: 25_000,
        Algorithm: "HeldKarp",
        IsOptimal: true,
        IsApproximate: false,
        MatrixProvider: "OSRM",
        NavigationLinks: [new RunNavigationLink(1, 1, 0, orderIds.Length + 1, "https://www.google.com/maps/dir/?api=1")]);

    private static DeliveryRun StartedWith(params Guid[] orderIds)
    {
        var run = DraftWith(orderIds);
        run.ApplyRoute(Route(orderIds), Now).IsSuccess.ShouldBeTrue();
        run.Approve().IsSuccess.ShouldBeTrue();
        run.Start(Now).IsSuccess.ShouldBeTrue();
        return run;
    }

    [Fact]
    public void Create_NormalizesDetails_StartsAsDraft()
    {
        var run = DeliveryRun.Create(Details());

        run.Status.ShouldBe(DeliveryRunStatus.Draft);
        run.Name.ShouldBe("Poranna Bydgoszcz");
        run.VehiclePlate.ShouldBe("CB 12345");
        run.HasRoutePlan.ShouldBeFalse();
    }

    [Fact]
    public void AddOrders_AppendsInSequence()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var run = DraftWith(a, b);

        run.AddOrders([c]).IsSuccess.ShouldBeTrue();

        run.Stops.Select(s => (s.OrderId, s.DeliverySequence)).ShouldBe([(a, 1), (b, 2), (c, 3)]);
        run.Stops.ShouldAllBe(s => s.Status == DeliveryStopStatus.Pending);
    }

    [Fact]
    public void AddOrders_OrderAlreadyInRun_Fails()
    {
        var order = Guid.CreateVersion7();
        var run = DraftWith(order);

        run.AddOrders([order]).Error.Code.ShouldBe("Delivery.OrderAlreadyInThisRun");
    }

    [Fact]
    public void AddOrders_OverStopLimit_Fails()
    {
        var run = DraftWith([.. Enumerable.Range(0, DeliveryRun.MaxStops).Select(_ => Guid.CreateVersion7())]);

        run.AddOrders([Guid.CreateVersion7()]).Error.Code.ShouldBe("Delivery.TooManyStops");
    }

    [Fact]
    public void RemoveOrder_RenumbersRemainingStops()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var run = DraftWith(a, b, c);

        run.RemoveOrder(b).IsSuccess.ShouldBeTrue();

        run.Stops.Select(s => (s.OrderId, s.DeliverySequence)).ShouldBe([(a, 1), (c, 2)]);
    }

    [Fact]
    public void ApplyRoute_ReordersStopsAndStoresPlan()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var run = DraftWith(a, b, c);

        run.ApplyRoute(Route(c, a, b), Now).IsSuccess.ShouldBeTrue();

        run.Stops.Select(s => s.OrderId).ShouldBe([c, a, b]);
        run.Stops.Select(s => s.EstimatedArrivalOffsetSeconds).ShouldBe([600d, 1200d, 1800d]);
        run.Stops[0].PlannedLocation.ShouldBe(new GeoPoint(53.12, 18.0));
        run.HasRoutePlan.ShouldBeTrue();
        run.OptimizationAlgorithm.ShouldBe("HeldKarp");
        run.OptimizedDurationSeconds.ShouldBe(3600);
        run.OptimizedAt.ShouldBe(Now);
        run.NavigationLinks.Count.ShouldBe(1);
    }

    [Fact]
    public void ApplyRoute_NotAPermutationOfStops_Fails()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7();
        var run = DraftWith(a, b);

        run.ApplyRoute(Route(a, a), Now).Error.Code.ShouldBe("Delivery.InvalidSequence");
        run.ApplyRoute(Route(a), Now).Error.Code.ShouldBe("Delivery.InvalidSequence");
        run.ApplyRoute(Route(a, Guid.CreateVersion7()), Now).Error.Code.ShouldBe("Delivery.InvalidSequence");
    }

    [Fact]
    public void AddOrders_AfterPlanning_ClearsStoredRoute()
    {
        var a = Guid.CreateVersion7();
        var run = DraftWith(a);
        run.ApplyRoute(Route(a), Now);

        run.AddOrders([Guid.CreateVersion7()]);

        run.HasRoutePlan.ShouldBeFalse();
        run.NavigationLinks.ShouldBeEmpty();
        run.Stops.ShouldAllBe(s => s.EstimatedArrivalOffsetSeconds == null && s.PlannedLocation == null);
    }

    [Fact]
    public void UpdateDetails_EndLocationChanged_ClearsStoredRoute()
    {
        var a = Guid.CreateVersion7();
        var run = DraftWith(a);
        run.ApplyRoute(Route(a), Now);

        run.UpdateDetails(Details(endLocationId: Guid.CreateVersion7())).IsSuccess.ShouldBeTrue();

        run.HasRoutePlan.ShouldBeFalse();
    }

    [Fact]
    public void Approve_WithoutStops_Fails() =>
        DeliveryRun.Create(Details()).Approve().Error.Code.ShouldBe("Delivery.RunHasNoStops");

    [Fact]
    public void Approve_WithoutRoutePlan_Fails() =>
        DraftWith(Guid.CreateVersion7()).Approve().Error.Code.ShouldBe("Delivery.RouteNotPlanned");

    [Fact]
    public void Approve_PlannedRun_LocksEditing()
    {
        var a = Guid.CreateVersion7();
        var run = DraftWith(a);
        run.ApplyRoute(Route(a), Now);

        run.Approve().IsSuccess.ShouldBeTrue();

        run.Status.ShouldBe(DeliveryRunStatus.Planned);
        run.AddOrders([Guid.CreateVersion7()]).Error.Code.ShouldBe("Delivery.RunNotEditable");
        run.RemoveOrder(a).Error.Code.ShouldBe("Delivery.RunNotEditable");
        run.ApplyRoute(Route(a), Now).Error.Code.ShouldBe("Delivery.RunNotEditable");
    }

    [Fact]
    public void Start_DraftRun_Fails() =>
        DraftWith(Guid.CreateVersion7()).Start(Now).Error.Code.ShouldBe("Delivery.InvalidTransition");

    [Fact]
    public void Cancel_InProgressRun_Fails() =>
        StartedWith(Guid.CreateVersion7()).Cancel().Error.Code.ShouldBe("Delivery.InvalidTransition");

    [Fact]
    public void MarkStopDelivered_BeforeStart_Fails()
    {
        var run = DraftWith(Guid.CreateVersion7());

        run.MarkStopDelivered(run.Stops[0].Id, Now).Error.Code.ShouldBe("Delivery.RunNotInProgress");
    }

    [Fact]
    public void MarkStop_LastStopCompleted_CompletesRun()
    {
        var run = StartedWith(Guid.CreateVersion7(), Guid.CreateVersion7());
        var later = Now.AddHours(2);

        run.MarkStopDelivered(run.Stops[0].Id, Now.AddHours(1)).IsSuccess.ShouldBeTrue();
        run.Status.ShouldBe(DeliveryRunStatus.InProgress);

        var failed = run.MarkStopFailed(run.Stops[1].Id, "  Brak odbiorcy ", later);

        failed.Value.Status.ShouldBe(DeliveryStopStatus.Failed);
        failed.Value.FailureReason.ShouldBe("Brak odbiorcy");
        run.Status.ShouldBe(DeliveryRunStatus.Completed);
        run.CompletedAt.ShouldBe(later);
        run.Stops[0].DeliveredAt.ShouldBe(Now.AddHours(1));
    }

    [Fact]
    public void MarkStop_AlreadyCompleted_Fails()
    {
        var run = StartedWith(Guid.CreateVersion7(), Guid.CreateVersion7());
        var stop = run.Stops[0].Id;
        run.MarkStopDelivered(stop, Now);

        run.MarkStopFailed(stop, "Brak odbiorcy", Now).Error.Code.ShouldBe("Delivery.StopAlreadyCompleted");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MarkStopFailed_WithoutReason_Fails(string reason)
    {
        var run = StartedWith(Guid.CreateVersion7());

        run.MarkStopFailed(run.Stops[0].Id, reason, Now).Error.Code.ShouldBe("Delivery.FailureReasonRequired");
        run.Stops[0].Status.ShouldBe(DeliveryStopStatus.Pending);
    }

    [Fact]
    public void MarkStop_UnknownStop_Fails() =>
        StartedWith(Guid.CreateVersion7()).MarkStopDelivered(Guid.CreateVersion7(), Now).Error.Code.ShouldBe("Delivery.StopNotFound");

    [Fact]
    public void Complete_WithPendingStops_FailsThemAsNotVisited()
    {
        Guid a = Guid.CreateVersion7(), b = Guid.CreateVersion7(), c = Guid.CreateVersion7();
        var run = StartedWith(a, b, c);
        run.MarkStopDelivered(run.Stops[0].Id, Now);

        var notVisited = run.Complete(Now.AddHours(3));

        notVisited.Value.ShouldBe([b, c], ignoreOrder: true);
        run.Status.ShouldBe(DeliveryRunStatus.Completed);
        run.Stops.Skip(1).ShouldAllBe(s => s.Status == DeliveryStopStatus.Failed && s.FailureReason == DeliveryRun.NotVisitedReason);
        run.Stops[0].Status.ShouldBe(DeliveryStopStatus.Delivered);
    }
}
