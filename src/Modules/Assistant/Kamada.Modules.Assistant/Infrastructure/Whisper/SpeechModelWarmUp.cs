using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Infrastructure.Whisper;

/// <summary>
/// Prepares the speech model in the background after startup, so the API is available immediately; until it is ready,
/// <c>GET /api/assistant/status</c> reports <c>speechToText: false</c> and typing still works.
/// </summary>
internal sealed partial class SpeechModelWarmUp(
    ISpeechToText speechToText,
    IOptions<AssistantOptions> options,
    ILogger<SpeechModelWarmUp> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var settings = options.Value;
        if (!settings.Enabled || !settings.Whisper.Enabled)
        {
            return;
        }

        try
        {
            // Model loading is partly synchronous: keep it off the startup path.
            await Task.Run(() => speechToText.PrepareAsync(stoppingToken), stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            LogPreparationFailed(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Speech model could not be prepared – dictation is unavailable until the API restarts")]
    private partial void LogPreparationFailed(Exception exception);
}
