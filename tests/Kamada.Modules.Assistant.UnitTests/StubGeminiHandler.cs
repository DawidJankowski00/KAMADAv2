using System.Net;
using System.Text;
using System.Text.Json;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Infrastructure.Gemini;
using Microsoft.Extensions.Logging.Abstractions;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Modules.Assistant.UnitTests;

/// <summary>Plays the Gemini API: records the request and answers with a canned status and body.</summary>
internal sealed class StubGeminiHandler(HttpStatusCode status, string body) : HttpMessageHandler
{
    public const string ApiKey = "test-gemini-key";

    public HttpRequestMessage? Request { get; private set; }

    public JsonDocument? RequestBody { get; private set; }

    /// <summary>A generateContent answer with one candidate whose text is <paramref name="text"/>.</summary>
    public static StubGeminiHandler Answering(string text) =>
        new(HttpStatusCode.OK, JsonSerializer.Serialize(new
        {
            candidates = new[] { new { content = new { role = "model", parts = new[] { new { text } } }, finishReason = "STOP" } },
            usageMetadata = new { promptTokenCount = 900, candidatesTokenCount = 60 },
        }));

    public static GeminiClient Client(HttpMessageHandler handler, string? apiKey = ApiKey) => new(
        new HttpClient(handler) { BaseAddress = new Uri("https://gemini.test/") },
        MsOptions.Create(new AssistantOptions { Enabled = true, Gemini = new GeminiOptions { ApiKey = apiKey } }),
        NullLogger<GeminiClient>.Instance);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Request = request;
        if (request.Content is not null)
        {
            RequestBody = JsonDocument.Parse(await request.Content.ReadAsStringAsync(cancellationToken));
        }

        return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
    }
}
