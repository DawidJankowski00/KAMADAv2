using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kamada.Modules.Assistant.Application;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;
using Polly.Timeout;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

/// <summary>What a Gemini call ended with; <see cref="Text"/> is set only for <see cref="GeminiOutcome.Answered"/>.</summary>
internal enum GeminiOutcome
{
    Answered,

    /// <summary>Network failure, timeout, rate limit or a server error (after retries).</summary>
    Unavailable,

    /// <summary>The API refused the key, the model or the request (401, 403, 404, 400).</summary>
    Refused,

    /// <summary>The model answered without usable text (blocked, empty, cut off).</summary>
    NoAnswer,
}

internal sealed record GeminiResult(GeminiOutcome Outcome, string? Text = null, HttpStatusCode? Status = null);

/// <summary>
/// Thin REST client of the Gemini API (<c>models.generateContent</c>, <c>models.get</c>). The API key travels in the
/// <c>x-goog-api-key</c> header (never in the URL, so request logs cannot leak it). Neither prompts nor answers are
/// logged – they hold clients' personal data.
/// </summary>
internal sealed partial class GeminiClient(HttpClient http, IOptions<AssistantOptions> options, ILogger<GeminiClient> logger)
{
    public const string HttpClientName = "assistant-gemini";

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public bool IsConfigured => options.Value.Gemini.IsConfigured;

    public async Task<GeminiResult> GenerateAsync(string model, GeminiRequest request, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent")
        {
            Content = JsonContent.Create(request, options: Json),
        };

        var (response, failure) = await SendAsync(message, cancellationToken);
        if (response is null)
        {
            return failure!;
        }

        using (response)
        {
            var body = await response.Content.ReadFromJsonAsync<GeminiResponse>(Json, cancellationToken);
            var candidate = body?.Candidates is [var first, ..] ? first : null;
            var text = candidate?.Content?.Parts is { } parts
                ? string.Concat(parts.Where(p => p.Thought != true).Select(p => p.Text))
                : null;

            LogAnswered(model, candidate?.FinishReason, body?.UsageMetadata?.PromptTokenCount, body?.UsageMetadata?.CandidatesTokenCount);
            return string.IsNullOrWhiteSpace(text)
                ? new GeminiResult(GeminiOutcome.NoAnswer)
                : new GeminiResult(GeminiOutcome.Answered, text);
        }
    }

    /// <summary>Whether the key works and the model exists – no inference, no cost.</summary>
    public async Task<GeminiResult> GetModelAsync(string model, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"v1beta/models/{Uri.EscapeDataString(model)}");
        var (response, failure) = await SendAsync(message, cancellationToken);
        response?.Dispose();
        return failure ?? new GeminiResult(GeminiOutcome.Answered);
    }

    private async Task<(HttpResponseMessage? Response, GeminiResult? Failure)> SendAsync(HttpRequestMessage message, CancellationToken cancellationToken)
    {
        if (!IsConfigured)
        {
            return (null, new GeminiResult(GeminiOutcome.Refused));
        }

        message.Headers.Add("x-goog-api-key", options.Value.Gemini.ApiKey);
        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or BrokenCircuitException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogUnavailable(ex);
            return (null, new GeminiResult(GeminiOutcome.Unavailable));
        }

        if (response.IsSuccessStatusCode)
        {
            return (response, null);
        }

        var status = response.StatusCode;
        var reason = await ReadErrorAsync(response, cancellationToken);
        response.Dispose();
        LogRefused((int)status, reason);
        var outcome = status is HttpStatusCode.TooManyRequests || (int)status >= 500 ? GeminiOutcome.Unavailable : GeminiOutcome.Refused;
        return (null, new GeminiResult(outcome, Status: status));
    }

    /// <summary>The API's own error status (e.g. <c>PERMISSION_DENIED</c>), never the request body.</summary>
    private static async Task<string?> ReadErrorAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(cancellationToken), cancellationToken: cancellationToken);
            return document.RootElement.TryGetProperty("error", out var error) && error.TryGetProperty("status", out var status)
                ? status.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Gemini {Model} answered ({FinishReason}, {PromptTokens} prompt / {AnswerTokens} answer tokens)")]
    private partial void LogAnswered(string model, string? finishReason, int? promptTokens, int? answerTokens);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini API unavailable")]
    private partial void LogUnavailable(Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Gemini API answered {StatusCode} {Reason}")]
    private partial void LogRefused(int statusCode, string? reason);
}
