using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.IntegrationTests.Customers;

/// <summary>Creates repeated with the same <c>RequestId</c> (the phone app's outbox retrying after a lost response).</summary>
public sealed class ClientRequestIdTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static AddressRequest Address(string building = "10", Guid? requestId = null) =>
        new(AddressType.Shipping, "Gdańska", building, null, "Bydgoszcz", "85-005", RequestId: requestId);

    private static CreateClientRequest NewClient(Guid? requestId)
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        return new CreateClientRequest($"client-{unique}", $"client-{unique}@example.com", "Ewa", "Nowicka", "+48 511 222 333", [Address()], requestId);
    }

    [Fact]
    public async Task Create_RepeatedRequestId_ReturnsTheClientCreatedFirst()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var request = NewClient(Guid.CreateVersion7());

        var first = await seller.PostJsonAsync("/api/clients", request);
        var repeated = await seller.PostJsonAsync("/api/clients", request);

        first.StatusCode.ShouldBe(HttpStatusCode.Created);
        repeated.StatusCode.ShouldBe(HttpStatusCode.Created);
        var created = await first.ReadAsAsync<ClientDto>();
        var replayed = await repeated.ReadAsAsync<ClientDto>();
        replayed.Id.ShouldBe(created.Id);
        replayed.Addresses.ShouldHaveSingleItem().Id.ShouldBe(created.Addresses.Single().Id);
    }

    [Fact]
    public async Task Create_ConcurrentRetries_CreateOneClient()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var request = NewClient(Guid.CreateVersion7());

        var responses = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => seller.PostJsonAsync("/api/clients", request)));

        responses.ShouldAllBe(r => r.StatusCode == HttpStatusCode.Created);
        var ids = await Task.WhenAll(responses.Select(async r => (await r.ReadAsAsync<ClientDto>()).Id));
        ids.Distinct().ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Create_SameDataWithoutRequestId_IsStillAConflict()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var request = NewClient(requestId: null);
        (await seller.PostJsonAsync("/api/clients", request)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var repeated = await seller.PostJsonAsync("/api/clients", request);

        repeated.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await repeated.ReadProblemCodeAsync()).ShouldBe("Clients.UsernameTaken");
    }

    [Fact]
    public async Task Create_EmptyRequestId_Returns400()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);

        var response = await seller.PostJsonAsync("/api/clients", NewClient(Guid.Empty));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AddAddress_RepeatedRequestId_AddsOneAddress()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var client = await (await seller.PostJsonAsync("/api/clients", NewClient(null))).ReadAsAsync<ClientDto>();
        var request = Address("22", Guid.CreateVersion7());

        var first = await (await seller.PostJsonAsync($"/api/clients/{client.Id}/addresses", request)).ReadAsAsync<AddressDto>();
        var repeated = await seller.PostJsonAsync($"/api/clients/{client.Id}/addresses", request);

        repeated.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await repeated.ReadAsAsync<AddressDto>()).Id.ShouldBe(first.Id);
        var details = await (await seller.GetAsync($"/api/clients/{client.Id}", Ct)).ReadAsAsync<ClientDto>();
        details.Addresses.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddAddress_RequestIdUsedForAnotherClient_Returns409()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var first = await (await seller.PostJsonAsync("/api/clients", NewClient(null))).ReadAsAsync<ClientDto>();
        var second = await (await seller.PostJsonAsync("/api/clients", NewClient(null))).ReadAsAsync<ClientDto>();
        var request = Address("22", Guid.CreateVersion7());
        (await seller.PostJsonAsync($"/api/clients/{first.Id}/addresses", request)).StatusCode.ShouldBe(HttpStatusCode.Created);

        var response = await seller.PostJsonAsync($"/api/clients/{second.Id}/addresses", request);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await response.ReadProblemCodeAsync()).ShouldBe("Requests.IdReused");
    }
}
