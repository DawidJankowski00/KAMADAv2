using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Infrastructure.Whisper;

/// <summary>
/// Starts preparing the Whisper model right after startup when the developer settings select it, so the first dictation
/// does not wait for the download. When Whisper is selected later, the first status or dictation request starts it.
/// Until it is ready, <c>GET /api/assistant/status</c> reports <c>speechToText: false</c> and typing still works.
/// </summary>
internal sealed partial class SpeechModelWarmUp(
    IAssistantSettings settings,
    IOptions<AssistantOptions> options,
    [FromKeyedServices(SpeechProvider.Whisper)] ISpeechToText whisper,
    ILogger<SpeechModelWarmUp> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            var current = await settings.GetAsync(stoppingToken);
            if (AssistantProviders.IsEnabled(options.Value, current)
                && current.SpeechProvider == SpeechProvider.Whisper
                && AssistantProviders.IsSpeechAllowed(options.Value, current))
            {
                await whisper.IsReadyAsync(stoppingToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogSettingsUnavailable(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Assistant settings could not be read at startup – Whisper is prepared on first use")]
    private partial void LogSettingsUnavailable(Exception exception);
}
