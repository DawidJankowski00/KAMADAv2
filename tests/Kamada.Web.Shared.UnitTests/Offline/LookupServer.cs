using System.Net;
using System.Net.Http.Json;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Offline;

namespace Kamada.Web.Shared.UnitTests.Offline;

/// <summary>Serves the two lookup lists with an ETag per version and answers 304 to the current tag.</summary>
internal sealed class LookupServer : HttpMessageHandler
{
    private int _version = 1;

    public List<ProductLookupDto> Products { get; } = [new(Guid.NewGuid(), "WODA-05", "Woda 0,5 l", 2.50m, 40, [])];

    public List<ClientLookupDto> Clients { get; } = [new(Guid.NewGuid(), "jan", "jan@example.com", "Jan", "Kowalski", "+48 600 000 000", [])];

    public bool Unreachable { get; set; }

    public List<(string Path, string? IfNoneMatch, HttpStatusCode Status)> Requests { get; } = [];

    public HttpClient CreateClient() => new(this, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };

    /// <summary>Changes the data, so the next request gets it with a new tag.</summary>
    public void Change() => _version++;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (Unreachable)
        {
            throw new HttpRequestException("Connection refused");
        }

        var path = request.RequestUri!.AbsolutePath;
        var ifNoneMatch = request.Headers.TryGetValues("If-None-Match", out var values) ? values.Single() : null;
        var etag = $"\"{path.Split('/')[2]}-v{_version}\"";
        object? body = path switch
        {
            "/" + OfflineLookup.ProductsUrl => Products,
            "/" + OfflineLookup.ClientsUrl => Clients,
            _ => null,
        };

        HttpResponseMessage response;
        if (body is null)
        {
            response = new HttpResponseMessage(HttpStatusCode.NotFound);
        }
        else if (ifNoneMatch == etag)
        {
            response = new HttpResponseMessage(HttpStatusCode.NotModified);
        }
        else
        {
            response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body, options: KamadaJson.Options) };
            response.Headers.ETag = new System.Net.Http.Headers.EntityTagHeaderValue(etag);
        }

        Requests.Add((path, ifNoneMatch, response.StatusCode));
        return Task.FromResult(response);
    }
}
