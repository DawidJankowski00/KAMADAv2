using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Shared.Contracts.Assistant;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application;

/// <summary>What the admin panel should offer. Cheap (no model calls); reachability of the model is reported by <c>/health</c>.</summary>
internal sealed class AssistantStatusQuery(IOptions<AssistantOptions> options, ISpeechToText speechToText)
{
    public AssistantStatusDto Get()
    {
        var settings = options.Value;
        return new AssistantStatusDto(
            settings.Enabled,
            SpeechToText: settings.Enabled && settings.Whisper.Enabled && speechToText.IsReady,
            settings.Enabled ? settings.Ollama.Model : null);
    }
}
