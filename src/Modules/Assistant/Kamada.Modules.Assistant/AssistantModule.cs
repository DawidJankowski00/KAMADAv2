using FluentValidation;
using Kamada.Modules.Assistant.Api;
using Kamada.Modules.Assistant.Application;
using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Application.Drafts;
using Kamada.Modules.Assistant.Application.Settings;
using Kamada.Modules.Assistant.Application.Transcriptions;
using Kamada.Modules.Assistant.Infrastructure;
using Kamada.Modules.Assistant.Infrastructure.Gemini;
using Kamada.Modules.Assistant.Infrastructure.Ollama;
using Kamada.Modules.Assistant.Infrastructure.Persistence;
using Kamada.Modules.Assistant.Infrastructure.Providers;
using Kamada.Modules.Assistant.Infrastructure.Settings;
using Kamada.Modules.Assistant.Infrastructure.Whisper;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Matching.Drafts;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using OllamaSharp;
using Polly;

namespace Kamada.Modules.Assistant;

/// <summary>
/// Entry point of the Assistant module (order drafts from dictated or typed notes): the only public type of the
/// implementation assembly. Its schema <c>assistant</c> holds only the developer settings; it reads clients and
/// products through <c>ICustomerDirectory</c> and <c>ICatalogLookup</c> and never writes them.
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

        services.AddModuleDbContext<AssistantDbContext>(AssistantDbContext.Schema);
        services.AddScoped<IAssistantDbContext>(sp => sp.GetRequiredService<AssistantDbContext>());
        services.AddSingleton<IAssistantSettings, CachedAssistantSettings>();

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

        services.AddHttpClient<GeminiClient>(GeminiClient.HttpClientName, (sp, http) =>
            {
                http.BaseAddress = sp.GetRequiredService<IOptions<AssistantOptions>>().Value.Gemini.BaseUrl;
                http.Timeout = Timeout.InfiniteTimeSpan; // the resilience pipeline below owns the timeouts
            })
            .AddResilienceHandler("gemini", (pipeline, context) =>
            {
                var gemini = context.ServiceProvider.GetRequiredService<IOptions<AssistantOptions>>().Value.Gemini;
                pipeline
                    .AddTimeout(gemini.TotalTimeout)
                    .AddRetry(new HttpRetryStrategyOptions
                    {
                        MaxRetryAttempts = 2,
                        Delay = TimeSpan.FromSeconds(1),
                        BackoffType = DelayBackoffType.Exponential,
                    })
                    .AddTimeout(gemini.AttemptTimeout);
            });

        services.AddHttpClient(WhisperSpeechToText.DownloadHttpClient, http => http.Timeout = TimeSpan.FromHours(1));
        SelectedSpeechToText.Register(services);
        services.AddHostedService<SpeechModelWarmUp>();
        services.AddScoped<TranscriptionHandler>();

        SelectedOrderExtractor.Register(services);
        SelectedModelProbe.Register(services);
        services.AddScoped<IOrderDraftSource, ModuleOrderDraftSource>();
        services.AddScoped<OrderDraftResolver>();
        services.AddScoped<OrderDraftHandler>();
        services.AddScoped<AssistantStatusQuery>();
        services.AddScoped<AssistantSettingsCommands>();
        services.AddScoped<ProviderConnectionTest>();
        services.AddValidatorsFromAssemblyContaining<OrderDraftHandler>(includeInternalTypes: true);

        if (configuration.GetValue<bool>($"{AssistantOptions.SectionName}:{nameof(AssistantOptions.Enabled)}"))
        {
            services.AddHealthChecks()
                .AddCheck<AssistantHealthCheck>("assistant", failureStatus: HealthStatus.Degraded);
        }

        return services;
    }

    public static IEndpointRouteBuilder MapAssistantEndpoints(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapOrderAssistantEndpoints().MapAssistantSettingsEndpoints();
}
