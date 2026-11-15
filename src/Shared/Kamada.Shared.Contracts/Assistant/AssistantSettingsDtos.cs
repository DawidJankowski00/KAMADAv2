namespace Kamada.Shared.Contracts.Assistant;

/// <summary>Who turns a note into an order draft.</summary>
public enum AssistantTextProvider
{
    Ollama,
    Gemini,
}

/// <summary>Who recognizes dictated notes (<see cref="Off"/>: typing only).</summary>
public enum AssistantSpeechProvider
{
    Off,
    Whisper,
    Gemini,
}

/// <summary>Developer settings of the order assistant (<c>GET/PUT /api/assistant/settings</c>, role Developer).</summary>
/// <param name="Enabled">Stored switch; the assistant works only when <see cref="AssistantAvailabilityDto.AssistantEnabled"/> is on too.</param>
/// <param name="Availability">What the server configuration allows (changed only by configuration, not here).</param>
public sealed record AssistantSettingsDto(
    bool Enabled,
    AssistantTextProvider TextProvider,
    string OllamaModel,
    string GeminiModel,
    AssistantSpeechProvider SpeechProvider,
    DateTimeOffset? UpdatedAt,
    AssistantAvailabilityDto Availability);

/// <param name="AssistantEnabled">Hard switch <c>Assistant:Enabled</c>.</param>
/// <param name="WhisperEnabled">Hard switch <c>Assistant:Whisper:Enabled</c> (local speech model allowed on this server).</param>
/// <param name="GeminiConfigured">An API key is configured (<c>Assistant:Gemini:ApiKey</c>); the key itself is never sent.</param>
public sealed record AssistantAvailabilityDto(bool AssistantEnabled, bool WhisperEnabled, bool GeminiConfigured);

public sealed record UpdateAssistantSettingsRequest(
    bool Enabled,
    AssistantTextProvider TextProvider,
    string OllamaModel,
    string GeminiModel,
    AssistantSpeechProvider SpeechProvider);
