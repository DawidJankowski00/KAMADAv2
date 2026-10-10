using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OllamaSharp.Models.Exceptions;

namespace Kamada.Modules.Assistant.Infrastructure.Ollama;

/// <summary>Lists the models downloaded on the Ollama server (<c>GET /api/tags</c>) – no inference.</summary>
internal sealed class OllamaModelProbe(IOllamaApiClient ollama, IOptions<AssistantOptions> options) : ILanguageModelProbe
{
    public async Task<LanguageModelProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var model = options.Value.Ollama.Model;
        try
        {
            var models = await ollama.ListLocalModelsAsync(cancellationToken);
            return new LanguageModelProbeResult(true, models.Any(m => IsSameModel(m.Name, model)), model);
        }
        catch (Exception ex) when (ex is HttpRequestException or OllamaException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            return new LanguageModelProbeResult(false, false, model);
        }
    }

    /// <summary>Ollama reports "gemma3:4b"; a model configured without a tag means ":latest".</summary>
    private static bool IsSameModel(string installed, string configured) =>
        string.Equals(installed, configured, StringComparison.OrdinalIgnoreCase)
        || (!configured.Contains(':', StringComparison.Ordinal)
            && string.Equals(installed, configured + ":latest", StringComparison.OrdinalIgnoreCase));
}
