using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Contracts.Assistant;

namespace Kamada.Modules.Assistant.Application.Settings;

internal static class AssistantSettingsMappings
{
    public static AssistantSettingsDto ToDto(this AssistantSettingsSnapshot s, AssistantOptions options) => new(
        s.Enabled,
        s.TextProvider.ToContract(),
        s.OllamaModel,
        s.GeminiModel,
        s.SpeechProvider.ToContract(),
        s.UpdatedAt,
        new AssistantAvailabilityDto(options.Enabled, options.Whisper.Enabled));

    public static AssistantTextProvider ToContract(this TextProvider provider) => provider switch
    {
        TextProvider.Ollama => AssistantTextProvider.Ollama,
        TextProvider.Gemini => AssistantTextProvider.Gemini,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static TextProvider ToDomain(this AssistantTextProvider provider) => provider switch
    {
        AssistantTextProvider.Ollama => TextProvider.Ollama,
        AssistantTextProvider.Gemini => TextProvider.Gemini,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static AssistantSpeechProvider ToContract(this SpeechProvider provider) => provider switch
    {
        SpeechProvider.Off => AssistantSpeechProvider.Off,
        SpeechProvider.Whisper => AssistantSpeechProvider.Whisper,
        SpeechProvider.Gemini => AssistantSpeechProvider.Gemini,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };

    public static SpeechProvider ToDomain(this AssistantSpeechProvider provider) => provider switch
    {
        AssistantSpeechProvider.Off => SpeechProvider.Off,
        AssistantSpeechProvider.Whisper => SpeechProvider.Whisper,
        AssistantSpeechProvider.Gemini => SpeechProvider.Gemini,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, null),
    };
}
