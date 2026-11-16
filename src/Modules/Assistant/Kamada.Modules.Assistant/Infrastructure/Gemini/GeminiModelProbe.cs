using System.Net;
using Kamada.Modules.Assistant.Application.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Assistant.Infrastructure.Gemini;

/// <summary>Reads the model's description (<c>models.get</c>): checks the key and the model name, costs no tokens.</summary>
internal sealed class GeminiModelProbe(IServiceScopeFactory scopeFactory, IAssistantSettings settings) : ILanguageModelProbe
{
    public async Task<LanguageModelProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var model = (await settings.GetAsync(cancellationToken)).GeminiModel;
        await using var scope = scopeFactory.CreateAsyncScope();
        var result = await scope.ServiceProvider.GetRequiredService<GeminiClient>().GetModelAsync(model, cancellationToken);
        return result switch
        {
            { Outcome: GeminiOutcome.Answered } => new LanguageModelProbeResult(true, true, model, "Gemini"),
            { Status: HttpStatusCode.NotFound } => new LanguageModelProbeResult(true, false, model, "Gemini"),
            _ => new LanguageModelProbeResult(false, false, model, "Gemini"),
        };
    }
}
