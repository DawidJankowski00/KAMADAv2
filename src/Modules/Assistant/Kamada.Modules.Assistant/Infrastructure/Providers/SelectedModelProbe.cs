using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Assistant.Infrastructure.Providers;

/// <summary>Probes the provider that currently reads notes (used by <c>/health</c>).</summary>
internal sealed class SelectedModelProbe(IAssistantSettings settings, IServiceProvider services) : ILanguageModelProbe
{
    public async Task<LanguageModelProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var provider = (await settings.GetAsync(cancellationToken)).TextProvider;
        return await services.GetRequiredKeyedService<ILanguageModelProbe>(provider).ProbeAsync(cancellationToken);
    }

    public static void Register(IServiceCollection services)
    {
        services.AddKeyedSingleton<ILanguageModelProbe, Ollama.OllamaModelProbe>(TextProvider.Ollama);
        services.AddKeyedSingleton<ILanguageModelProbe, Gemini.GeminiModelProbe>(TextProvider.Gemini);
        services.AddSingleton<ILanguageModelProbe, SelectedModelProbe>();
    }
}
