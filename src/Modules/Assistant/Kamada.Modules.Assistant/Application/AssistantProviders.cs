using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;

namespace Kamada.Modules.Assistant.Application;

/// <summary>What the developer settings select, limited by what the configuration allows.</summary>
internal static class AssistantProviders
{
    /// <summary>The assistant is on: hard switch <c>Assistant:Enabled</c> and the stored switch.</summary>
    public static bool IsEnabled(AssistantOptions options, AssistantSettingsSnapshot settings) =>
        options.Enabled && settings.Enabled;

    /// <summary>Dictation has a provider this server may use (it may still be loading).</summary>
    public static bool IsSpeechAllowed(AssistantOptions options, AssistantSettingsSnapshot settings) => settings.SpeechProvider switch
    {
        SpeechProvider.Whisper => options.Whisper.Enabled,
        SpeechProvider.Gemini => options.Gemini.IsConfigured,
        _ => false,
    };
}
