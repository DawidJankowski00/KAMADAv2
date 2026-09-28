using System.Net;
using System.Text;

namespace Kamada.Modules.RouteOptimization.UnitTests.Matrix;

/// <summary>Answers every request with a canned JSON body and records what was sent.</summary>
internal sealed class StubHttpHandler(Func<HttpRequestMessage, string> respond, HttpStatusCode status = HttpStatusCode.OK) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    public List<string> Bodies { get; } = [];

    public HttpClient Client(string baseUrl) => new(this) { BaseAddress = new Uri(baseUrl) };

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken));
        return new HttpResponseMessage(status)
        {
            Content = new StringContent(respond(request), Encoding.UTF8, "application/json"),
        };
    }
}
