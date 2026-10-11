using FluentValidation;
using Kamada.Modules.Assistant.Api;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Application.Drafts;
using Kamada.Modules.Assistant.Application.Transcriptions;
using Kamada.Modules.Assistant.Infrastructure;
using Kamada.Modules.Assistant.Infrastructure.Ollama;
using Kamada.Modules.Assistant.Infrastructure.Whisper;
using Kamada.Shared.Matching.Drafts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using OllamaSharp;

namespace Kamada.Modules.Assistant;

/// <summary>
/// Entry point of the Assistant module (order drafts from dictated or typed notes, local AI only):
/// the only public type of the implementation assembly. Stateless – no database schema; it reads clients and
/// products through <c>ICustomerDirectory</c> and <c>ICatalogLookup</c> and never writes.
/// </summary>
public static class AssistantModule
{
    public const string Name = "Assistant";

    private const string OllamaHttpClient = "assistant-ollama";

    public static IServiceCollection AddAssistantModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<AssistantOptions>()
            .Bind(configuration.GetSection(AssistantOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddHttpClient(OllamaHttpClient, (sp, http) =>
        {
            var ollama = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.Ollama;
            http.BaseAddress = ollama.BaseUrl;
            http.Timeout = ollama.RequestTimeout;
        });
        services.AddSingleton(sp => new OllamaApiClient(
            sp.GetRequiredService<IHttpClientFactory>().CreateClient(OllamaHttpClient),
            sp.GetRequiredService<IOptions<AssistantOptions>>().Value.Ollama.Model));
        services.AddSingleton<IOllamaApiClient>(sp => sp.GetRequiredService<OllamaApiClient>());
        services.AddKeyedSingleton<IChatClient>(AssistantServiceKeys.ChatClient, (sp, _) => sp.GetRequiredService<OllamaApiClient>());

        services.AddHttpClient(WhisperSpeechToText.DownloadHttpClient, http => http.Timeout = TimeSpan.FromHours(1));
        services.AddSingleton<ISpeechToText, WhisperSpeechToText>();
        services.AddHostedService<SpeechModelWarmUp>();
        services.AddScoped<TranscriptionHandler>();

        services.AddScoped<IOrderExtractor, OllamaOrderExtractor>();
        services.AddSingleton<ILanguageModelProbe, OllamaModelProbe>();
        services.AddScoped<IOrderDraftSource, ModuleOrderDraftSource>();
        services.AddScoped<OrderDraftResolver>();
        services.AddScoped<OrderDraftHandler>();
        services.AddSingleton<AssistantStatusQuery>();
        services.AddValidatorsFromAssemblyContaining<OrderDraftHandler>(includeInternalTypes: true);

        if (configuration.GetValue<bool>($"{AssistantOptions.SectionName}:{nameof(AssistantOptions.Enabled)}"))
        {
            services.AddHealthChecks()
                .AddCheck<AssistantHealthCheck>("assistant", failureStatus: HealthStatus.Degraded);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapOrderAssistantEndpoints();
}
