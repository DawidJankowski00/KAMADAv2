using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Delivery;

namespace Kamada.IntegrationTests.Delivery;

public sealed class RouteLocationTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_KnownAddress_IsGeocoded()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var location = await admin.CreateRouteLocationAsync(buildingNumber: "3");

        location.IsActive.ShouldBeTrue();
        location.HasCoordinates.ShouldBeTrue();
        location.Latitude.ShouldBe(FakeGeocodingService.LocationFor("3").Latitude);
    }

    [Fact]
    public async Task Create_UnknownAddress_IsSavedWithoutCoordinates_AndCanBePinned()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var location = await admin.CreateRouteLocationAsync(street: $"{FakeGeocodingService.UnknownStreetMarker} 9");
        location.HasCoordinates.ShouldBeFalse();

        var pinned = await admin.PutJsonAsync($"/api/route-locations/{location.Id}/location", new SetRouteLocationPinRequest(53.1, 18.1));

        pinned.StatusCode.ShouldBe(HttpStatusCode.OK);
        var dto = await pinned.ReadAsAsync<RouteLocationDto>();
        (dto.Latitude, dto.Longitude).ShouldBe((53.1, 18.1));
    }

    [Fact]
    public async Task Create_DuplicateName_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var existing = await admin.CreateRouteLocationAsync();

        var response = await admin.PostJsonAsync("/api/route-locations", new RouteLocationRequest(existing.Name, "Inna", "2", "Bydgoszcz", "85-001"));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Delivery.LocationNameTaken");
    }

    [Fact]
    public async Task Create_InvalidPostalCode_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync("/api/route-locations", new RouteLocationRequest("Baza", "Fordońska", "1", "Bydgoszcz", "85719"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Deactivate_HidesLocationFromDefaultList()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var location = await admin.CreateRouteLocationAsync();

        (await admin.PostAsync($"/api/route-locations/{location.Id}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var active = await (await admin.GetAsync("/api/route-locations", Ct)).ReadAsAsync<List<RouteLocationDto>>();
        var all = await (await admin.GetAsync("/api/route-locations?includeInactive=true", Ct)).ReadAsAsync<List<RouteLocationDto>>();
        active.ShouldNotContain(l => l.Id == location.Id);
        all.Single(l => l.Id == location.Id).IsActive.ShouldBeFalse();
    }

    [Fact]
    public async Task List_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        (await courier.GetAsync("/api/route-locations", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
