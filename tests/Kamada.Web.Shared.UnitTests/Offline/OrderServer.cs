using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Contracts.Ordering;
using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.UnitTests.Offline;

/// <summary>
/// Plays the API for the outbox: creates clients, addresses and orders (remembering request ids like the real API) and can
/// fail a chosen step with a transport error or a ProblemDetails response.
/// </summary>
internal sealed class OrderServer : HttpMessageHandler
{
    private readonly Dictionary<Guid, object> _byRequestId = [];
    private long _nextNumber = 100;

    public List<(string Method, string Path, JsonElement Body)> Requests { get; } = [];

    /// <summary>Path prefix (e.g. <c>/api/orders</c>) → what to answer instead of the normal response.</summary>
    public Dictionary<string, Failure> Failures { get; } = [];

    /// <summary>Orders for this client are refused with <c>Products.InsufficientStock</c>.</summary>
    public Guid? RefuseOrdersFor { get; set; }

    public HttpClient CreateClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };

    public IEnumerable<string> Paths => Requests.Select(r => r.Path);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath;
        var body = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken)).RootElement.Clone();
        Requests.Add((request.Method.Method, path, body));

        if (Failures.FirstOrDefault(f => path.StartsWith(f.Key, StringComparison.Ordinal)).Value is { } failure)
        {
            if (failure.Status == 0)
            {
                throw new HttpRequestException("Connection lost");
            }

            return new HttpResponseMessage((HttpStatusCode)failure.Status)
            {
                Content = new StringContent($$"""{"title":"Refused","status":{{failure.Status}},"code":"{{failure.Code}}"}""", null, "application/problem+json"),
            };
        }

        if (path == "/api/orders" && RefuseOrdersFor is { } refused && body.GetProperty("clientId").GetGuid() == refused)
        {
            return new HttpResponseMessage(HttpStatusCode.Conflict)
            {
                Content = new StringContent("""{"title":"Refused","status":409,"code":"Products.InsufficientStock"}""", null, "application/problem+json"),
            };
        }

        object created = path switch
        {
            "/api/clients" => Replay(body, () => CreateClient(JsonSerializer.Deserialize<CreateClientRequest>(body, KamadaJson.Options)!)),
            _ when path.EndsWith("/addresses", StringComparison.Ordinal) =>
                Replay(body, () => CreateAddress(JsonSerializer.Deserialize<AddressRequest>(body, KamadaJson.Options)!)),
            "/api/orders" => Replay(body, () => PlaceOrder(JsonSerializer.Deserialize<PlaceOrderRequest>(body, KamadaJson.Options)!)),
            _ => throw new InvalidOperationException(path),
        };
        return new HttpResponseMessage(HttpStatusCode.Created) { Content = JsonContent.Create(created, created.GetType(), options: KamadaJson.Options) };
    }

    private object Replay(JsonElement body, Func<object> create)
    {
        if (body.TryGetProperty("requestId", out var id) && id.ValueKind == JsonValueKind.String)
        {
            var requestId = id.GetGuid();
            if (!_byRequestId.TryGetValue(requestId, out var existing))
            {
                existing = create();
                _byRequestId[requestId] = existing;
            }

            return existing;
        }

        return create();
    }

    private static AddressDto CreateAddress(AddressRequest a) =>
        new(Guid.NewGuid(), a.Type, a.Street, a.BuildingNumber, a.ApartmentNumber, a.City, a.PostalCode, a.Country, null, null, null, null);

    private static ClientDto CreateClient(CreateClientRequest c) =>
        new(Guid.NewGuid(), c.Username, c.Email, c.Name, c.Surname, c.PhoneNumber, true, DateTimeOffset.UtcNow, [.. (c.Addresses ?? []).Select(CreateAddress)]);

    private OrderDto PlaceOrder(PlaceOrderRequest o) =>
        new(Guid.NewGuid(), _nextNumber++, o.ClientId, null, OrderStatus.New, 10m, o.Notes,
            new ShippingAddressDto(o.ShippingAddressId, "Jan Kowalski", "+48 600 000 000", "Długa", "1", null, "Gdańsk", "80-001", "Polska", null, null),
            [], DateTimeOffset.UtcNow, null);

    /// <summary>Status 0 = the connection breaks.</summary>
    public sealed record Failure(int Status, string? Code = null);
}
