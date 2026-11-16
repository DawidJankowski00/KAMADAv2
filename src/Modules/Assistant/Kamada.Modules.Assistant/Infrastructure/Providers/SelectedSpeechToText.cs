using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Infrastructure.Providers;

/// <summary>
/// The <see cref="ISpeechToText"/> the application uses: the provider the developer settings select, if this server
/// allows it. Only a selected Whisper is ever asked whether it is ready, so its model is downloaded only when chosen.
/// </summary>
internal sealed class SelectedSpeechToText(
    IAssistantSettings settings,
    IOptions<AssistantOptions> options,
    IServiceProvider services) : ISpeechToText
{
    public async ValueTask<bool> IsReadyAsync(CancellationToken cancellationToken) =>
        await SelectedAsync(cancellationToken) is { } selected && await selected.IsReadyAsync(cancellationToken);

    public async Task<Result<Transcription>> TranscribeAsync(ReadOnlyMemory<byte> wav, AssistantLanguage language, CancellationToken cancellationToken) =>
        await SelectedAsync(cancellationToken) is { } selected
            ? await selected.TranscribeAsync(wav, language, cancellationToken)
            : AssistantErrors.SpeechToTextDisabled;

    /// <summary>Registers one implementation per <see cref="SpeechProvider"/> (keyed) and this selector as the default.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddSingleton<Whisper.WhisperSpeechToText>();
        services.AddKeyedSingleton<ISpeechToText>(SpeechProvider.Whisper, (sp, _) => sp.GetRequiredService<Whisper.WhisperSpeechToText>());
        services.AddKeyedScoped<ISpeechToText, Gemini.GeminiSpeechToText>(SpeechProvider.Gemini);
        services.AddScoped<ISpeechToText, SelectedSpeechToText>();
    }

    private async Task<ISpeechToText?> SelectedAsync(CancellationToken cancellationToken)
    {
        var current = await settings.GetAsync(cancellationToken);
        return AssistantProviders.IsSpeechAllowed(options.Value, current)
            ? services.GetRequiredKeyedService<ISpeechToText>(current.SpeechProvider)
            : null;
    }
}
