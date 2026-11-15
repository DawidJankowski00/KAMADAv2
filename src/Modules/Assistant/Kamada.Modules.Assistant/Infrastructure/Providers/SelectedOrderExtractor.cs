using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Kernel.Results;
using Kamada.Shared.Matching.Drafts;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.Modules.Assistant.Infrastructure.Providers;

/// <summary>The <see cref="IOrderExtractor"/> the application uses: delegates to the provider the developer settings select.</summary>
internal sealed class SelectedOrderExtractor(IAssistantSettings settings, IServiceProvider services) : IOrderExtractor
{
    public async Task<Result<ExtractedOrder>> ExtractAsync(string text, AssistantLanguage language, CancellationToken cancellationToken)
    {
        var provider = (await settings.GetAsync(cancellationToken)).TextProvider;
        return await services.GetRequiredKeyedService<IOrderExtractor>(provider).ExtractAsync(text, language, cancellationToken);
    }

    /// <summary>Registers one implementation per <see cref="TextProvider"/> (keyed) and this selector as the default.</summary>
    public static void Register(IServiceCollection services)
    {
        services.AddKeyedScoped<IOrderExtractor, Ollama.OllamaOrderExtractor>(TextProvider.Ollama);
        services.AddKeyedScoped<IOrderExtractor, Gemini.GeminiOrderExtractor>(TextProvider.Gemini);
        services.AddScoped<IOrderExtractor, SelectedOrderExtractor>();
    }
}
