using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.IntegrationTests.Workforce;

/// <summary>
/// A seller may call only what taking an order needs (policy <c>SalesAccess</c>); everything else stays admin-only.
/// Requests carry empty bodies and random ids: an allowed endpoint answers 400/404/409, never 403.
/// </summary>
public sealed class SellerAccessTests(KamadaApiFactory factory)
{
    private static readonly Guid Id = Guid.NewGuid();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> SalesEndpoints() => new()
    {
        { "GET", "/api/categories" },
        { "GET", "/api/products" },
        { "GET", "/api/clients" },
        { "GET", $"/api/clients/{Id}" },
        { "POST", "/api/clients" },
        { "POST", $"/api/clients/{Id}/addresses" },
        { "POST", "/api/orders" },
        { "GET", "/api/orders/mine" },
        { "GET", $"/api/orders/{Id}" },
        { "GET", "/api/assistant/status" },
        { "POST", "/api/assistant/order-drafts" },
        { "POST", "/api/assistant/transcriptions" },
    };

    public static TheoryData<string, string> AdminOnlyEndpoints() => new()
    {
        { "POST", "/api/categories" },
        { "PUT", $"/api/categories/{Id}" },
        { "DELETE", $"/api/categories/{Id}" },
        { "GET", $"/api/products/{Id}" },
        { "POST", "/api/products" },
        { "PUT", $"/api/products/{Id}" },
        { "PUT", $"/api/products/{Id}/stock" },
        { "POST", $"/api/products/{Id}/deactivate" },
        { "POST", $"/api/products/{Id}/activate" },
        { "PUT", $"/api/clients/{Id}" },
        { "POST", $"/api/clients/{Id}/deactivate" },
        { "POST", $"/api/clients/{Id}/activate" },
        { "PUT", $"/api/clients/{Id}/addresses/{Id}" },
        { "DELETE", $"/api/clients/{Id}/addresses/{Id}" },
        { "PUT", $"/api/clients/{Id}/addresses/{Id}/location" },
        { "POST", $"/api/clients/{Id}/addresses/{Id}/geocode" },
        { "GET", "/api/orders" },
        { "GET", "/api/orders/status-counts" },
        { "POST", $"/api/orders/{Id}/ready-for-delivery" },
        { "POST", $"/api/orders/{Id}/cancel" },
        { "GET", $"/api/orders/{Id}/payments" },
        { "POST", $"/api/orders/{Id}/payments" },
        { "GET", $"/api/payments/{Id}" },
        { "POST", $"/api/payments/{Id}/confirm" },
        { "POST", $"/api/payments/{Id}/refund" },
        { "GET", "/api/delivery-runs" },
        { "POST", "/api/delivery-runs" },
        { "GET", "/api/route-locations" },
        { "GET", "/api/workers" },
        { "POST", "/api/workers" },
        { "GET", "/api/courier/delivery-runs" },
    };

    [Theory]
    [MemberData(nameof(SalesEndpoints))]
    public async Task SalesEndpoint_AsSeller_IsNotForbidden(string method, string url)
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);

        var response = await SendAsync(seller, method, url);

        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
        response.StatusCode.ShouldNotBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(SalesEndpoints))]
    public async Task SalesEndpoint_AsCourier_Returns403(string method, string url)
    {
        var (courier, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        (await SendAsync(courier, method, url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(AdminOnlyEndpoints))]
    public async Task AdminOnlyEndpoint_AsSeller_Returns403(string method, string url)
    {
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);

        (await SendAsync(seller, method, url)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Products_AsSeller_ListsOnlyActiveProducts()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync();
        (await admin.PostAsync($"/api/products/{product.Id}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await (await seller.GetAsync($"/api/products?search={product.Sku}&isActive=false", Ct)).ReadAsAsync<PagedResult<ProductDto>>();

        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Clients_AsSeller_ListsOnlyActiveClients()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var (clientId, _) = await admin.CreateClientAsync();
        var client = await (await admin.GetAsync($"/api/clients/{clientId}", Ct)).ReadAsAsync<ClientDto>();
        (await admin.PostAsync($"/api/clients/{clientId}/deactivate", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var page = await (await seller.GetAsync($"/api/clients?search={client.Username}", Ct)).ReadAsAsync<PagedResult<ClientSummaryDto>>();

        page.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task PlaceOrder_AsSellerForNewClient_Returns201()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (seller, _) = await factory.CreateSignedInClientAsync(WorkerRole.Seller);
        var product = await admin.CreateProductAsync(stock: 5);

        var (clientId, addressId) = await seller.CreateClientAsync();
        var response = await seller.PlaceOrderRawAsync(clientId, addressId, (product.Id, 2));

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await admin.GetStockAsync(product.Id)).ShouldBe(3);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient client, string method, string url)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT")
        {
            // The form endpoint answers 415 to JSON before authorization runs.
            request.Content = url.EndsWith("/transcriptions", StringComparison.Ordinal)
                ? new MultipartFormDataContent()
                : new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        }

        return client.SendAsync(request, Ct);
    }
}
