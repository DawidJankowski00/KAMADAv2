using System.Collections.Concurrent;
using System.Net;
using System.Text;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Plays the Gemini API for the real <c>GeminiClient</c> (its primary HTTP handler): recognizes any recording as
/// <see cref="Transcript"/> and reads any note as two coffees for Jan Kowalski.
/// </summary>
internal sealed class FakeGeminiApi : HttpMessageHandler
{
    public const string ApiKey = "integration-tests-gemini-placeholder";

    public const string Transcript = "Dwie kawy dla Jana Kowalskiego";

    private const string Extraction =
        """{"client":{"name":"Jan","surname":"Kowalski","phone":null,"email":null},"address":null,"items":[{"product":"kawa","quantity":2}],"notes":null}""";

    public ConcurrentQueue<string> Paths { get; } = new();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Paths.Enqueue(request.RequestUri!.AbsolutePath);
        if (!request.Headers.TryGetValues("x-goog-api-key", out var keys) || keys.Single() != ApiKey)
        {
            return Json(HttpStatusCode.Forbidden, """{"error":{"code":403,"status":"PERMISSION_DENIED"}}""");
        }

        if (request.Method == HttpMethod.Get)
        {
            return Json(HttpStatusCode.OK, """{"name":"models/fake"}""");
        }

        var body = await request.Content!.ReadAsStringAsync(cancellationToken);
        var answer = body.Contains("\"inlineData\"", StringComparison.Ordinal)
            ? $$"""{"text":"{{Transcript}}","language":"pl"}"""
            : Extraction;
        var escaped = System.Text.Json.JsonSerializer.Serialize(answer);
        return Json(HttpStatusCode.OK, $$"""{"candidates":[{"content":{"role":"model","parts":[{"text":{{escaped}}}]},"finishReason":"STOP"}]}""");
    }

    private static HttpResponseMessage Json(HttpStatusCode status, string body) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
