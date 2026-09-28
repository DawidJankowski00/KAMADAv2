using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.IntegrationTests.Customers;

public sealed class ClientTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AddressRequest Address(string street = "Gdańska", string building = "10") =>
        new(AddressType.Shipping, street, building, "4", "Bydgoszcz", "85-005");

    private static CreateClientRequest NewClient(params AddressRequest[] addresses)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        return new CreateClientRequest($"client-{unique}", $"client-{unique}@example.com", "Ewa", "Nowicka", "+48 511 222 333", addresses);
    }

    private static async Task<ClientDto> CreateAsync(HttpClient admin, CreateClientRequest request)
    {
        var response = await admin.PostJsonAsync("/api/clients", request);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<ClientDto>();
    }

    [Fact]
    public async Task Create_WithAddress_GeocodesAddress()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var client = await CreateAsync(admin, NewClient(Address()));

        var address = client.Addresses.Single();
        address.HasCoordinates.ShouldBeTrue();
        address.CoordinatesSource.ShouldBe(CoordinatesSource.Geocoded);
        address.Latitude.ShouldBe(FakeGeocodingService.LocationFor("10").Latitude);
    }

    [Fact]
    public async Task AddAddress_GeocodingFails_SavesAddressWithoutCoordinates_ThenManualPinWorks()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var client = await CreateAsync(admin, NewClient());

        var added = await admin.PostJsonAsync(
            $"/api/clients/{client.Id}/addresses",
            Address(street: $"{FakeGeocodingService.UnknownStreetMarker} 1"));

        added.StatusCode.ShouldBe(HttpStatusCode.Created);
        var address = await added.ReadAsAsync<AddressDto>();
        address.HasCoordinates.ShouldBeFalse();

        var retry = await admin.PostAsync($"/api/clients/{client.Id}/addresses/{address.Id}/geocode", null, Ct);
        (await retry.ReadProblemCodeAsync()).ShouldBe("Clients.GeocodingFailed");

        var pinned = await admin.PutJsonAsync(
            $"/api/clients/{client.Id}/addresses/{address.Id}/location",
            new SetAddressLocationRequest(53.1301, 18.0102));

        pinned.StatusCode.ShouldBe(HttpStatusCode.OK);
        var pinnedAddress = await pinned.ReadAsAsync<AddressDto>();
        pinnedAddress.CoordinatesSource.ShouldBe(CoordinatesSource.Manual);
        pinnedAddress.Latitude.ShouldBe(53.1301);
    }

    [Fact]
    public async Task UpdateAddress_NewBuilding_IsGeocodedAgain()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var client = await CreateAsync(admin, NewClient(Address(building: "10")));
        var addressId = client.Addresses.Single().Id;

        var response = await admin.PutJsonAsync($"/api/clients/{client.Id}/addresses/{addressId}", Address(building: "22A"));

        var updated = await response.ReadAsAsync<AddressDto>();
        updated.BuildingNumber.ShouldBe("22A");
        updated.Latitude.ShouldBe(FakeGeocodingService.LocationFor("22A").Latitude);
    }

    [Fact]
    public async Task ArchiveAddress_RemovesItFromClient()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var client = await CreateAsync(admin, NewClient(Address(), Address(building: "11")));
        var archived = client.Addresses[0].Id;

        (await admin.DeleteAsync($"/api/clients/{client.Id}/addresses/{archived}", Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var reloaded = await (await admin.GetAsync($"/api/clients/{client.Id}", Ct)).ReadAsAsync<ClientDto>();
        reloaded.Addresses.Select(a => a.Id).ShouldNotContain(archived);
        reloaded.Addresses.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Create_DuplicateEmail_Returns409()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var first = NewClient();
        await CreateAsync(admin, first);

        var duplicate = await admin.PostJsonAsync("/api/clients", NewClient() with { Email = first.Email.ToUpperInvariant() });

        duplicate.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await duplicate.ReadProblemCodeAsync()).ShouldBe("Clients.EmailTaken");
    }

    [Fact]
    public async Task Create_InvalidPolishPostalCode_Returns400()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);

        var response = await admin.PostJsonAsync("/api/clients", NewClient(Address() with { PostalCode = "85005" }));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task List_SearchBySurname_ReturnsSummaryWithAddressCount()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var surname = $"Szukany{Guid.NewGuid():N}"[..20];
        await CreateAsync(admin, NewClient(Address(), Address(building: "3")) with { Surname = surname });

        var page = await (await admin.GetAsync($"/api/clients?search={surname}", Ct)).ReadAsAsync<PagedResult<ClientSummaryDto>>();

        page.Items.Single().AddressCount.ShouldBe(2);
    }

    [Fact]
    public async Task Clients_AsCourier_Returns403()
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        (await courier.GetAsync("/api/clients", Ct)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
