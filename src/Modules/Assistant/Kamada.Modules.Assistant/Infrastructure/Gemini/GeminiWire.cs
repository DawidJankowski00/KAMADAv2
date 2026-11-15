using System.Text.Json;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

// Request and response bodies of models.generateContent (Gemini API v1beta, JSON in camelCase). Only the fields this
// module uses; unknown response fields are ignored.

internal sealed record GeminiRequest(
    IReadOnlyList<GeminiContent> Contents,
    GeminiGenerationConfig GenerationConfig,
    GeminiContent? SystemInstruction = null);

/// <param name="Role"><c>user</c> or <c>model</c>; none for the system instruction.</param>
internal sealed record GeminiContent(IReadOnlyList<GeminiPart> Parts, string? Role = null)
{
    public static GeminiContent User(params GeminiPart[] parts) => new(parts, "user");

    public static GeminiContent Model(string text) => new([new GeminiPart(text)], "model");
}

/// <param name="Thought">Set on parts with the model's reasoning, which are not part of the answer.</param>
internal sealed record GeminiPart(string? Text = null, GeminiBlob? InlineData = null, bool? Thought = null);

/// <param name="Data">Base64 of the bytes.</param>
internal sealed record GeminiBlob(string MimeType, string Data);

/// <param name="Temperature">Ignored by the newest models (they are deterministic enough with a schema).</param>
internal sealed record GeminiGenerationConfig(
    double? Temperature = null,
    string? ResponseMimeType = null,
    JsonElement? ResponseJsonSchema = null);

internal sealed record GeminiResponse(
    IReadOnlyList<GeminiCandidate>? Candidates,
    GeminiUsage? UsageMetadata,
    string? ModelVersion);

internal sealed record GeminiCandidate(GeminiContent? Content, string? FinishReason);

internal sealed record GeminiUsage(int? PromptTokenCount, int? CandidatesTokenCount, int? ThoughtsTokenCount);
