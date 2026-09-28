using System.Net;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Shared.Contracts.Payments;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Arranges catalog, customer and order data through the public API (as an admin would).
/// </summary>
internal static class TestData
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<ProductDto> CreateProductAsync(this HttpClient admin, int stock = 10, decimal price = 25.50m)
    {
        var sku = $"IT-{Guid.NewGuid():N}"[..15].ToUpperInvariant();
        var response = await admin.PostJsonAsync("/api/products", new CreateProductRequest($"Produkt {sku}", null, sku, price, stock, []));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<ProductDto>();
    }

    public static async Task<int> GetStockAsync(this HttpClient admin, Guid productId) =>
        (await (await admin.GetAsync($"/api/products/{productId}", Ct)).ReadAsAsync<ProductDto>()).StockQuantity;

    /// <summary>Client with one geocoded shipping address; returns client id and address id.</summary>
    public static async Task<(Guid ClientId, Guid AddressId)> CreateClientAsync(
        this HttpClient admin,
        AddressType addressType = AddressType.Shipping,
        string buildingNumber = "10")
    {
        var unique = Guid.NewGuid().ToString("N")[..10];
        var response = await admin.PostJsonAsync("/api/clients", new CreateClientRequest(
            $"c-{unique}",
            $"c-{unique}@example.com",
            "Adam",
            "Klient",
            "+48 600 111 222",
            [new AddressRequest(addressType, "Dworcowa", buildingNumber, null, "Bydgoszcz", "85-010")]));
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var client = await response.ReadAsAsync<ClientDto>();
        return (client.Id, client.Addresses.Single().Id);
    }

    public static Task<HttpResponseMessage> PlaceOrderRawAsync(this HttpClient admin, Guid clientId, Guid addressId, params (Guid ProductId, int Quantity)[] lines) =>
        admin.PostJsonAsync("/api/orders", new PlaceOrderRequest(
            clientId,
            addressId,
            [.. lines.Select(l => new OrderLineRequest(l.ProductId, l.Quantity))]));

    public static async Task<OrderDto> PlaceOrderAsync(this HttpClient admin, params (Guid ProductId, int Quantity)[] lines)
    {
        var (clientId, addressId) = await admin.CreateClientAsync();
        var response = await admin.PlaceOrderRawAsync(clientId, addressId, lines);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return await response.ReadAsAsync<OrderDto>();
    }

    /// <summary>Places an order for a fresh product and pays it in cash (status Paid).</summary>
    public static async Task<OrderDto> CreatePaidOrderAsync(this HttpClient admin)
    {
        var product = await admin.CreateProductAsync();
        var order = await admin.PlaceOrderAsync((product.Id, 1));
        (await admin.PostJsonAsync($"/api/orders/{order.Id}/payments", new RegisterPaymentRequest(PaymentProvider.Cash)))
            .StatusCode.ShouldBe(HttpStatusCode.Created);
        return await admin.GetOrderAsync(order.Id);
    }

    public static async Task<OrderDto> GetOrderAsync(this HttpClient admin, Guid orderId) =>
        await (await admin.GetAsync($"/api/orders/{orderId}", Ct)).ReadAsAsync<OrderDto>();
}
