using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Assistant;
using Kamada.Web.Shared.Auth;
using Kamada.Web.Shared.Connectivity;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MudBlazor;
using MudBlazor.Services;

namespace Kamada.Web.Shared;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers session handling, the authenticated <see cref="ApiClient"/> and MudBlazor for a KAMADA frontend.
    /// </summary>
    public static IServiceCollection AddKamadaClient(this IServiceCollection services, KamadaClientOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<TokenStore>();
        services.AddSingleton<AuthService>();
        services.AddSingleton<RoleContext>();
        services.AddTransient<AuthorizingHandler>();
        services.AddSingleton<ConnectionMonitor>();
        services.AddSingleton<BrowserNetworkEvents>();
        services.AddTransient<IOrderAssistant, ServerOrderAssistant>();

        services.AddHttpClient(AuthService.AnonymousClientName, http => http.BaseAddress = options.ApiBaseUrl);
        services.AddHttpClient<ApiClient>(http =>
            {
                http.BaseAddress = options.ApiBaseUrl;
                // Longer than the API's own limit for local AI inference (CPU-only models can take a minute or two).
                http.Timeout = TimeSpan.FromMinutes(3);
            })
            .AddHttpMessageHandler<AuthorizingHandler>();

        services.AddAuthorizationCore();
        services.AddCascadingAuthenticationState();
        services.AddSingleton<SessionAuthenticationStateProvider>();
        services.AddSingleton<AuthenticationStateProvider>(sp => sp.GetRequiredService<SessionAuthenticationStateProvider>());

        services.AddMudServices(config =>
        {
            config.SnackbarConfiguration.PositionClass = options.SnackbarPosition;
            config.SnackbarConfiguration.VisibleStateDuration = 4000;
            config.SnackbarConfiguration.PreventDuplicates = false;
        });

        return services;
    }
}
