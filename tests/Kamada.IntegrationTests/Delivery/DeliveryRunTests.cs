using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Contracts.Payments;

namespace Kamada.IntegrationTests.Delivery;

/// <summary>Admin side of delivery runs: composing, planning and approving.</summary>
public sealed class DeliveryRunTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Admin, TestWorker Courier, RouteLocationDto Location)> ArrangeAsync()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var courier = await factory.CreateWorkerAsync(WorkerRole.Courier);
        return (admin, courier, await admin.CreateRouteLocationAsync());
    }

    [Fact]
    public async Task Create_WithReadyOrders_AssignsOrdersToRun()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(2);

        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);

        run.Status.ShouldBe(DeliveryRunStatus.Draft);
        run.Courier.Id.ShouldBe(courier.Id);
        run.VehiclePlate.ShouldBe("CB 12345");
        run.Stops.Select(s => (s.OrderId, s.Sequence)).ShouldBe([(orders[0].Id, 1), (orders[1].Id, 2)]);
        run.Stops.ShouldAllBe(s => s.Status == DeliveryStopStatus.Pending && s.OrderStatus == OrderStatus.AssignedToRun);
        run.Stops[0].RecipientName.ShouldBe("Adam Klient");
        run.Stops[0].Items.Single().Quantity.ShouldBe(1);
        run.Stops[0].HasCoordinates.ShouldBeTrue();
        run.RoutePlan.ShouldBeNull();
        (await admin.GetOrderAsync(orders[0].Id)).Status.ShouldBe(OrderStatus.AssignedToRun);
    }

    [Fact]
    public async Task Create_WithOrderNotReadyForDelivery_Returns409_AndAssignsNothing()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var ready = await admin.CreateReadyOrderAsync();
        var paid = await admin.CreatePaidOrderAsync();

        var response = await admin.CreateRunRawAsync(courier.Id, location.Id, [ready.Id, paid.Id]);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.OrdersNotReady");
        (await admin.GetOrderAsync(ready.Id)).Status.ShouldBe(OrderStatus.ReadyForDelivery);
    }

    [Fact]
    public async Task AddOrders_OrderAlreadyInAnotherRun_Returns409()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var order = await admin.CreateReadyOrderAsync();
        await admin.CreateRunAsync(courier.Id, location.Id, [order.Id]);
        var second = await admin.CreateRunAsync(courier.Id, location.Id);

        var response = await admin.PostJsonAsync($"/api/delivery-runs/{second.Id}/orders", new AddRunOrdersRequest([order.Id]));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.OrdersNotReady");
    }

    [Fact]
    public async Task Create_WithAdminAsCourier_Returns400()
    {
        var (admin, adminWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var location = await admin.CreateRouteLocationAsync();

        var response = await admin.CreateRunRawAsync(adminWorker.Id, location.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Workers.NotACourier");
    }

    [Fact]
    public async Task Create_WithInactiveCourier_Returns409()
    {
        var (admin, _, location) = await ArrangeAsync();
        var inactive = await factory.CreateWorkerAsync(WorkerRole.Courier, isActive: false);

        var response = await admin.CreateRunRawAsync(inactive.Id, location.Id);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Workers.CourierInactive");
    }

    [Fact]
    public async Task RemoveOrder_ReturnsOrderToReadyForDelivery()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(2);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);

        var response = await admin.DeleteAsync($"/api/delivery-runs/{run.Id}/orders/{orders[0].Id}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsAsync<DeliveryRunDto>();
        updated.Stops.Single().OrderId.ShouldBe(orders[1].Id);
        updated.Stops.Single().Sequence.ShouldBe(1);
        (await admin.GetOrderAsync(orders[0].Id)).Status.ShouldBe(OrderStatus.ReadyForDelivery);
    }

    [Fact]
    public async Task Optimize_ReturnsProposalComparedWithCurrentOrder_WithoutSaving()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(4);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);

        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/optimize", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var preview = await response.ReadAsAsync<RouteOptimizationPreviewDto>();
        preview.Optimized.Stops.Count.ShouldBe(4);
        preview.Optimized.Stops.Select(s => s.OrderId).ShouldBe(orders.Select(o => o.Id), ignoreOrder: true);
        preview.Optimized.Algorithm.ShouldBe("BruteForce");
        preview.Optimized.IsOptimal.ShouldBeTrue();
        preview.Optimized.IsApproximate.ShouldBeTrue(); // Haversine in tests
        preview.Optimized.MatrixProvider.ShouldBe("Haversine");
        preview.Optimized.TotalDurationSeconds.ShouldBeLessThanOrEqualTo(preview.Current.TotalDurationSeconds);
        preview.Current.Stops.Select(s => s.OrderId).ShouldBe(orders.Select(o => o.Id));
        preview.Current.Algorithm.ShouldBe("Manual");
        preview.Optimized.NavigationLinks.Single().Url.ShouldStartWith("https://www.google.com/maps/dir/?api=1&origin=");

        // Arrival estimates grow along the route (driving + service time at earlier stops).
        var arrivals = preview.Optimized.Stops.Select(s => s.ArrivalOffsetSeconds).ToList();
        arrivals.ShouldBe(arrivals.Order().ToList());
        (await admin.GetRunAsync(run.Id)).RoutePlan.ShouldBeNull();
    }

    [Fact]
    public async Task ApplyOptimization_StoresOrderEstimatesAndLinks()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(3);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);
        var preview = await (await admin.PostAsync($"/api/delivery-runs/{run.Id}/optimize", null, Ct)).ReadAsAsync<RouteOptimizationPreviewDto>();

        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/apply-optimization", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var applied = await response.ReadAsAsync<DeliveryRunDto>();
        applied.Stops.Select(s => s.OrderId).ShouldBe(preview.Optimized.Stops.Select(s => s.OrderId));
        applied.Stops.Select(s => s.Sequence).ShouldBe([1, 2, 3]);
        applied.Stops.ShouldAllBe(s => s.EstimatedArrivalOffsetSeconds > 0 && s.NavigationUrl != null);
        applied.Stops[0].NavigationUrl!.ShouldContain("dir_action=navigate");
        var plan = applied.RoutePlan.ShouldNotBeNull();
        plan.DurationSeconds.ShouldBe(preview.Optimized.TotalDurationSeconds, 1e-6);
        plan.Algorithm.ShouldBe("BruteForce");
        plan.IsApproximate.ShouldBeTrue();
        applied.NavigationLinks.Single().Part.ShouldBe(1);

        // Persisted as returned (PostgreSQL keeps microseconds, .NET ticks are 100 ns).
        var reloaded = await admin.GetRunAsync(run.Id);
        reloaded.Stops.ShouldBeEquivalentTo(applied.Stops);
        reloaded.NavigationLinks.ShouldBeEquivalentTo(applied.NavigationLinks);
        (reloaded.RoutePlan! with { PlannedAt = plan.PlannedAt }).ShouldBe(plan);
        reloaded.RoutePlan.PlannedAt.ShouldBe(plan.PlannedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task SetSequence_ManualOrder_IsStoredWithRecomputedTimes()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(3);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);
        Guid[] manual = [orders[2].Id, orders[0].Id, orders[1].Id];

        var response = await admin.PutJsonAsync($"/api/delivery-runs/{run.Id}/sequence", new SetRunSequenceRequest(manual));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var updated = await response.ReadAsAsync<DeliveryRunDto>();
        updated.Stops.Select(s => s.OrderId).ShouldBe(manual);
        updated.RoutePlan!.Algorithm.ShouldBe("Manual");
        updated.RoutePlan.IsOptimal.ShouldBeFalse();
    }

    [Fact]
    public async Task SetSequence_MissingOrder_Returns400()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var orders = await admin.CreateReadyOrdersAsync(2);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [.. orders.Select(o => o.Id)]);

        var response = await admin.PutJsonAsync($"/api/delivery-runs/{run.Id}/sequence", new SetRunSequenceRequest([orders[0].Id]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.InvalidSequence");
    }

    [Fact]
    public async Task Optimize_OrderAddressWithoutCoordinates_Returns400WithOrderNumber()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var located = await admin.CreateReadyOrderAsync();
        var unlocated = await admin.CreateReadyOrderAsync(street: $"{FakeGeocodingService.UnknownStreetMarker} 3");
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [located.Id, unlocated.Id]);
        run.Stops.Single(s => s.OrderId == unlocated.Id).HasCoordinates.ShouldBeFalse();

        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/optimize", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.StopsWithoutCoordinates");
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain($"#{unlocated.Number}");
    }

    [Fact]
    public async Task Optimize_AfterAddressPinnedManually_UsesCurrentCoordinates()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var product = await admin.CreateProductAsync();
        var (clientId, addressId) = await admin.CreateClientAsync(street: $"{FakeGeocodingService.UnknownStreetMarker} 4");
        var order = await (await admin.PlaceOrderRawAsync(clientId, addressId, (product.Id, 1))).ReadAsAsync<OrderDto>();
        await admin.PostJsonAsync($"/api/orders/{order.Id}/payments", new RegisterPaymentRequest(PaymentProvider.Cash));
        await admin.PostAsync($"/api/orders/{order.Id}/ready-for-delivery", null, Ct);
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [order.Id]);

        // The order snapshot has no coordinates; the admin pins the client's address afterwards.
        (await admin.PutJsonAsync($"/api/clients/{clientId}/addresses/{addressId}/location", new SetAddressLocationRequest(53.13, 18.01)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/optimize", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stop = (await response.ReadAsAsync<RouteOptimizationPreviewDto>()).Optimized.Stops.Single();
        (stop.Latitude, stop.Longitude).ShouldBe((53.13, 18.01));
    }

    [Fact]
    public async Task Approve_WithoutRoutePlan_Returns409()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var order = await admin.CreateReadyOrderAsync();
        var run = await admin.CreateRunAsync(courier.Id, location.Id, [order.Id]);

        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/approve", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.RouteNotPlanned");
    }

    [Fact]
    public async Task Approved_Run_CannotBeEdited()
    {
        var (admin, courier, _) = await ArrangeAsync();
        var run = await admin.CreatePlannedRunAsync(courier.Id);
        var order = await admin.CreateReadyOrderAsync();

        var response = await admin.PostJsonAsync($"/api/delivery-runs/{run.Id}/orders", new AddRunOrdersRequest([order.Id]));

        run.Status.ShouldBe(DeliveryRunStatus.Planned);
        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.RunNotEditable");
    }

    [Fact]
    public async Task Cancel_PlannedRun_ReturnsOrdersToReadyForDelivery()
    {
        var (admin, courier, _) = await ArrangeAsync();
        var run = await admin.CreatePlannedRunAsync(courier.Id);

        var response = await admin.PostAsync($"/api/delivery-runs/{run.Id}/cancel", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<DeliveryRunDto>()).Status.ShouldBe(DeliveryRunStatus.Cancelled);
        foreach (var stop in run.Stops)
        {
            (await admin.GetOrderAsync(stop.OrderId)).Status.ShouldBe(OrderStatus.ReadyForDelivery);
        }
    }

    [Fact]
    public async Task List_FiltersByCourierAndStatus()
    {
        var (admin, courier, location) = await ArrangeAsync();
        var planned = await admin.CreatePlannedRunAsync(courier.Id);
        await admin.CreateRunAsync(courier.Id, location.Id);

        var page = await (await admin.GetAsync($"/api/delivery-runs?courierId={courier.Id}&status=Planned", Ct))
            .ReadAsAsync<PagedResult<DeliveryRunSummaryDto>>();

        var summary = page.Items.ShouldHaveSingleItem();
        summary.Id.ShouldBe(planned.Id);
        summary.StopCount.ShouldBe(2);
        summary.CourierName.ShouldBe($"Test {nameof(WorkerRole.Courier)}");
        summary.PlannedDurationSeconds.ShouldNotBeNull();
    }

    [Fact]
    public async Task AdminEndpoints_AsCourier_Return403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        (await courier.GetAsync("/api/delivery-runs", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await courier.PostAsync($"/api/delivery-runs/{Guid.NewGuid()}/optimize", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Get_UnknownRun_Returns404()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.GetAsync($"/api/delivery-runs/{Guid.NewGuid()}", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.RunNotFound");
    }
}
