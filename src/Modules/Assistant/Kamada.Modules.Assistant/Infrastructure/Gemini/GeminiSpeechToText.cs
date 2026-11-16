using System.Diagnostics;
using System.Text.Json;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

/// <summary>
/// Speech recognition with Google Gemini: the WAV recording goes inline (base64, ≤ 2 MB – far below the inline
/// limit) with an instruction to transcribe verbatim, and the answer comes in a small JSON schema. The recording is
/// sent to Google; nothing is stored or logged here.
/// </summary>
internal sealed partial class GeminiSpeechToText(
    GeminiClient gemini,
    IAssistantSettings settings,
    ILogger<GeminiSpeechToText> logger) : ISpeechToText
{
    internal const string Instruction = """
        You transcribe short voice notes dictated by a salesperson who is taking an order: a client's name, phone,
        address, products and quantities. Write down exactly what is said, in the language it is spoken in. Do not
        translate, correct, summarize or add anything. Write numbers as digits. If nothing is said, return an empty text.
        Answer with JSON: "text" = the transcript, "language" = "pl" or "en".
        """;

    private static readonly JsonElement Schema = JsonDocument.Parse("""
        {
          "type": "object",
          "properties": {
            "text": { "type": "string" },
            "language": { "type": "string", "enum": ["pl", "en"] }
          },
          "required": ["text", "language"]
        }
        """).RootElement.Clone();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>No local model to prepare: ready whenever an API key is configured.</summary>
    public ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) => ValueTask.FromResult(gemini.IsConfigured);

    public async Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken)
    {
        var model = (await settings.GetAsync(cancellationToken)).GeminiModel;
        var stopwatch = Stopwatch.StartNew();
        var result = await gemini.GenerateAsync(model, Request(wav, language), cancellationToken);
        if (result.Outcome is GeminiOutcome.Unavailable or GeminiOutcome.Refused)
        {
            return AssistantErrors.SpeechToTextUnavailable;
        }

        var answer = Parse(result.Text);
        LogTranscription(model, stopwatch.ElapsedMilliseconds);
        var fallbackLanguage = language == AssistantLanguage.Auto ? "pl" : language.ToCode();
        return new Transcription(
            answer?.Text?.Trim() ?? string.Empty,
            answer?.Language is "pl" or "en" ? answer.Language : fallbackLanguage);
    }

    internal static GeminiRequest Request(ReadOnlyMemory<byte> wav, AssistantLanguage language)
    {
        var hint = language switch
        {
            AssistantLanguage.Polish => "The recording is in Polish.",
            AssistantLanguage.English => "The recording is in English.",
            _ => "The recording is in Polish or English.",
        };

        return new GeminiRequest(
            [GeminiContent.User(new GeminiPart(InlineData: new GeminiBlob("audio/wav", Convert.ToBase64String(wav.Span))), new GeminiPart(hint))],
            new GeminiGenerationConfig(Temperature: 0, ResponseMimeType: "application/json", ResponseJsonSchema: Schema),
            SystemInstruction: new GeminiContent([new GeminiPart(Instruction)]));
    }

    private static SpeechAnswer? Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<SpeechAnswer>(text, Json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Speech recognition by Gemini {Model} took {ElapsedMs} ms")]
    private partial void LogTranscription(string model, long elapsedMs);

    private sealed record SpeechAnswer(string? Text, string? Language);
}
