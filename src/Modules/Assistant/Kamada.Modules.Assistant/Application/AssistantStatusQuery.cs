using Kamada.Shared.Contracts.Assistant;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Assistant.Application;

/// <summary>What the admin panel should offer. Cheap (configuration only); reachability is reported by <c>/health</c>.</summary>
internal sealed class AssistantStatusQuery(IOptions<AssistantOptions> options)
{
    public AssistantStatusDto Get()
    {
        var settings = options.Value;
        return new AssistantStatusDto(settings.Enabled, SpeechToText: false, settings.Enabled ? settings.Ollama.Model : null);
    }
}
