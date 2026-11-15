using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Application.Settings;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application;

/// <summary>What the admin panel should offer. Cheap (no model calls); reachability of the model is reported by <c>/health</c>.</summary>
internal sealed class AssistantStatusQuery(IOptions<AssistantOptions> options, IAssistantSettings assistantSettings, ISpeechToText speechToText)
{
    public async Task<AssistantStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var config = options.Value;
        var settings = await assistantSettings.GetAsync(cancellationToken);
        if (!AssistantProviders.IsEnabled(config, settings))
        {
            return new AssistantStatusDto(false, false, null);
        }

        // Asking a selected Whisper starts its download; the panel polls the status until dictation is ready.
        var speech = AssistantProviders.IsSpeechAllowed(config, settings) && await speechToText.IsReadyAsync(cancellationToken);
        return new AssistantStatusDto(
            true,
            speech,
            settings.TextModel,
            settings.TextProvider.ToContract(),
            speech ? settings.SpeechProvider.ToContract() : null);
    }
}
