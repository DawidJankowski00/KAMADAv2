using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Delivery.Domain.Runs;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Delivery;
using Kamada.Shared.Contracts.Ordering;
using DeliveryRunStatus = Kamada.Shared.Contracts.Delivery.DeliveryRunStatus;
using DeliveryStopStatus = Kamada.Shared.Contracts.Delivery.DeliveryStopStatus;

namespace Kamada.IntegrationTests.Delivery;

/// <summary>Courier app API: own runs only, run lifecycle on the road.</summary>
public sealed class CourierRunTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<(HttpClient Admin, HttpClient Courier, DeliveryRunDto Run)> PlannedRunAsync(int stops = 3, DateOnly? plannedDate = null)
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (courier, courierWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);
        var run = await admin.CreatePlannedRunAsync(courierWorker.Id, stops, plannedDate);
        return (admin, courier, run);
    }

    private static async Task<DeliveryRunDto> PostOkAsync(HttpClient client, string url, object? body = null)
    {
        var response = body is null ? await client.PostAsync(url, null, Ct) : await client.PostJsonAsync(url, body);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.ReadAsAsync<DeliveryRunDto>();
    }

    [Fact]
    public async Task FullScenario_PlanStartDeliverFail_CompletesRunAndUpdatesOrders()
    {
        var (admin, courier, run) = await PlannedRunAsync(stops: 3);
        var url = $"/api/courier/delivery-runs/{run.Id}";

        var current = await (await courier.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>();
        current.ShouldHaveSingleItem().Id.ShouldBe(run.Id);

        var details = await (await courier.GetAsync(url, Ct)).ReadAsAsync<DeliveryRunDto>();
        details.Stops.Count.ShouldBe(3);
        details.NavigationLinks.ShouldNotBeEmpty();
        details.Stops.ShouldAllBe(s => s.PhoneNumber != string.Empty && s.Items.Count == 1 && s.EstimatedArrivalOffsetSeconds != null);

        var started = await PostOkAsync(courier, $"{url}/start");
        started.Status.ShouldBe(DeliveryRunStatus.InProgress);
        started.StartedAt.ShouldNotBeNull();
        started.Stops.ShouldAllBe(s => s.OrderStatus == OrderStatus.OutForDelivery);

        var afterFirst = await PostOkAsync(courier, $"{url}/stops/{run.Stops[0].Id}/deliver");
        afterFirst.Stops[0].Status.ShouldBe(DeliveryStopStatus.Delivered);
        afterFirst.Stops[0].DeliveredAt.ShouldNotBeNull();

        var afterSecond = await PostOkAsync(courier, $"{url}/stops/{run.Stops[1].Id}/fail", new FailStopRequest("Brak odbiorcy pod adresem"));
        afterSecond.Stops[1].Status.ShouldBe(DeliveryStopStatus.Failed);
        afterSecond.Stops[1].FailureReason.ShouldBe("Brak odbiorcy pod adresem");
        afterSecond.Status.ShouldBe(DeliveryRunStatus.InProgress);

        var completed = await PostOkAsync(courier, $"{url}/stops/{run.Stops[2].Id}/deliver");
        completed.Status.ShouldBe(DeliveryRunStatus.Completed);
        completed.CompletedAt.ShouldNotBeNull();

        (await admin.GetOrderAsync(run.Stops[0].OrderId)).Status.ShouldBe(OrderStatus.Delivered);
        (await admin.GetOrderAsync(run.Stops[1].OrderId)).Status.ShouldBe(OrderStatus.DeliveryFailed);
        (await admin.GetOrderAsync(run.Stops[2].OrderId)).Status.ShouldBe(OrderStatus.Delivered);

        var finished = await (await courier.GetAsync("/api/courier/delivery-runs?scope=Completed", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>();
        var summary = finished.ShouldHaveSingleItem();
        (summary.DeliveredCount, summary.FailedCount).ShouldBe((2, 1));
    }

    [Fact]
    public async Task FailedOrder_CanBePlannedAgainInANewRun()
    {
        var (admin, courier, run) = await PlannedRunAsync(stops: 1);
        var url = $"/api/courier/delivery-runs/{run.Id}";
        await PostOkAsync(courier, $"{url}/start");
        await PostOkAsync(courier, $"{url}/stops/{run.Stops[0].Id}/fail", new FailStopRequest("Adres nieosiągalny"));
        var orderId = run.Stops[0].OrderId;

        (await admin.PostAsync($"/api/orders/{orderId}/ready-for-delivery", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var again = await admin.CreateRunAsync(run.Courier.Id, run.StartLocation.Id, [orderId]);

        again.Stops.Single().OrderStatus.ShouldBe(OrderStatus.AssignedToRun);
    }

    [Fact]
    public async Task Complete_WithPendingStops_FailsThemAsNotVisited()
    {
        var (admin, courier, run) = await PlannedRunAsync(stops: 2);
        var url = $"/api/courier/delivery-runs/{run.Id}";
        await PostOkAsync(courier, $"{url}/start");
        await PostOkAsync(courier, $"{url}/stops/{run.Stops[0].Id}/deliver");

        var completed = await PostOkAsync(courier, $"{url}/complete");

        completed.Status.ShouldBe(DeliveryRunStatus.Completed);
        completed.Stops[1].Status.ShouldBe(DeliveryStopStatus.Failed);
        completed.Stops[1].FailureReason.ShouldBe(DeliveryRun.NotVisitedReason);
        (await admin.GetOrderAsync(run.Stops[1].OrderId)).Status.ShouldBe(OrderStatus.DeliveryFailed);
    }

    [Fact]
    public async Task Fail_WithoutReason_Returns400()
    {
        var (_, courier, run) = await PlannedRunAsync(stops: 1);
        var url = $"/api/courier/delivery-runs/{run.Id}";
        await PostOkAsync(courier, $"{url}/start");

        var response = await courier.PostJsonAsync($"{url}/stops/{run.Stops[0].Id}/fail", new FailStopRequest(" "));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deliver_BeforeStart_Returns409()
    {
        var (_, courier, run) = await PlannedRunAsync(stops: 1);

        var response = await courier.PostAsync($"/api/courier/delivery-runs/{run.Id}/stops/{run.Stops[0].Id}/deliver", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.RunNotInProgress");
    }

    [Fact]
    public async Task Start_Twice_Returns409()
    {
        var (_, courier, run) = await PlannedRunAsync(stops: 1);
        await PostOkAsync(courier, $"/api/courier/delivery-runs/{run.Id}/start");

        var response = await courier.PostAsync($"/api/courier/delivery-runs/{run.Id}/start", null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.InvalidTransition");
    }

    [Fact]
    public async Task OtherCouriersRun_IsNotFound_ForReadAndActions()
    {
        var (_, _, run) = await PlannedRunAsync(stops: 1);
        var (otherCourier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);
        var url = $"/api/courier/delivery-runs/{run.Id}";

        (await otherCourier.GetAsync(url, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherCourier.PostAsync($"{url}/start", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await otherCourier.PostAsync($"{url}/stops/{run.Stops[0].Id}/deliver", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);

        var list = await (await otherCourier.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>();
        list.ShouldNotContain(r => r.Id == run.Id);
    }

    [Fact]
    public async Task DraftRun_IsNotVisibleToItsCourier()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (courier, courierWorker) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);
        var location = await admin.CreateRouteLocationAsync();
        var draft = await admin.CreateRunAsync(courierWorker.Id, location.Id);

        (await courier.GetAsync($"/api/courier/delivery-runs/{draft.Id}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await (await courier.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>())
            .ShouldBeEmpty();
    }

    [Fact]
    public async Task List_FutureRun_IsUpcomingNotCurrent()
    {
        var (_, courier, run) = await PlannedRunAsync(stops: 1, plannedDate: DeliveryTestData.Today.AddDays(3));

        var current = await (await courier.GetAsync("/api/courier/delivery-runs?scope=Current", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>();
        var upcoming = await (await courier.GetAsync("/api/courier/delivery-runs?scope=Upcoming", Ct)).ReadAsAsync<List<DeliveryRunSummaryDto>>();

        current.ShouldBeEmpty();
        upcoming.ShouldHaveSingleItem().Id.ShouldBe(run.Id);
    }

    [Fact]
    public async Task CourierEndpoints_AsAdmin_Return403()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        (await admin.GetAsync("/api/courier/delivery-runs", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task CourierEndpoints_Anonymous_Return401()
    {
        var anonymous = factory.CreateClient();

        (await anonymous.GetAsync("/api/courier/delivery-runs", Ct)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }
}
