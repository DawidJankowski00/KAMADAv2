using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
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
        var enabled = config.Enabled && settings.Enabled;
        return new AssistantStatusDto(
            enabled,
            SpeechToText: enabled && settings.SpeechProvider == SpeechProvider.Whisper && config.Whisper.Enabled && speechToText.IsReady,
            enabled ? settings.TextModel : null);
    }
}
