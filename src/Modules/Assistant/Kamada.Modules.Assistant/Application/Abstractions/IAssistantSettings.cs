using Kamada.Modules.Assistant.Domain.Settings;

namespace Kamada.Modules.Assistant.Application.Abstractions;

/// <summary>The developer settings as read at a moment (immutable).</summary>
internal sealed record AssistantSettingsSnapshot(
    bool Enabled,
    TextProvider TextProvider,
    string OllamaModel,
    string GeminiModel,
    SpeechProvider SpeechProvider,
    DateTimeOffset? UpdatedAt)
{
    /// <summary>Model of the provider that reads notes.</summary>
    public string TextModel => TextProvider == TextProvider.Gemini ? GeminiModel : OllamaModel;
}

/// <summary>
/// Developer settings of the assistant, read on every request from a short in-memory cache (they change rarely and a
/// request must not wait for the database). Saving on this instance invalidates the cache at once; other API instances
/// see the change within <c>CacheDuration</c>.
/// </summary>
internal interface IAssistantSettings
{
    /// <summary>Creates the settings row from the configuration the first time.</summary>
    Task<AssistantSettingsSnapshot> GetAsync(CancellationToken cancellationToken);

    void Invalidate();
}
