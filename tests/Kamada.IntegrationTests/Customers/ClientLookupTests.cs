using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.IntegrationTests.Customers;

public sealed class ClientLookupTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Lookup_AsSeller_ListsActiveClientsWithShippingAddressesOnly()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var (clientId, shippingId) = await admin.CreateClientAsync();
        (await admin.PostJsonAsync($"/api/clients/{clientId}/addresses",
            new AddressRequest(AddressType.Billing, "Długa", "1", null, "Bydgoszcz", "85-010"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        var (inactiveId, _) = await admin.CreateClientAsync();
        (await admin.PostAsync($"/api/clients/{inactiveId}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var response = await seller.GetAsync("/api/clients/lookup", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.ETag.ShouldNotBeNull();
        var clients = await response.ReadAsAsync<List<ClientLookupDto>>();
        var client = clients.Single(c => c.Id == clientId);
        client.ShippingAddresses.ShouldHaveSingleItem().Id.ShouldBe(shippingId);
        clients.ShouldNotContain(c => c.Id == inactiveId);
    }

    [Fact]
    public async Task Lookup_IfNoneMatchCurrentTag_Returns304()
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);

        var notModified = await seller.AnswersNotModifiedForCurrentTagAsync("/api/clients/lookup");

        notModified.ShouldBeTrue();
    }

    [Fact]
    public async Task Lookup_IfNoneMatchAfterNewAddress_ReturnsNewData()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (clientId, _) = await admin.CreateClientAsync();
        var etag = (await admin.GetAsync("/api/clients/lookup", Ct)).Headers.ETag!;

        (await admin.PostJsonAsync($"/api/clients/{clientId}/addresses",
            new AddressRequest(AddressType.Shipping, "Długa", "2", null, "Bydgoszcz", "85-010"))).StatusCode.ShouldBe(HttpStatusCode.Created);
        var response = await admin.GetIfNoneMatchAsync("/api/clients/lookup", etag);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.ReadAsAsync<List<ClientLookupDto>>()).Single(c => c.Id == clientId).ShippingAddresses.Count.ShouldBe(2);
    }
}
