using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;

namespace Kamada.Modules.Assistant.UnitTests;

/// <summary>Developer settings fixed for a test (no database).</summary>
internal sealed class StaticAssistantSettings(AssistantSettingsSnapshot snapshot) : IAssistantSettings
{
    public StaticAssistantSettings(
        TextProvider textProvider = TextProvider.Ollama,
        SpeechProvider speechProvider = SpeechProvider.Whisper,
        string ollamaModel = "gemma3:4b",
        string geminiModel = "gemini-3.5-flash-lite",
        bool enabled = true)
        : this(new AssistantSettingsSnapshot(enabled, textProvider, ollamaModel, geminiModel, speechProvider, null))
    {
    }

    public AssistantSettingsSnapshot Snapshot { get; set; } = snapshot;

    public Task<AssistantSettingsSnapshot> GetAsync(CancellationToken cancellationToken) => Task.FromResult(Snapshot);

    public void Invalidate()
    {
    }
}
