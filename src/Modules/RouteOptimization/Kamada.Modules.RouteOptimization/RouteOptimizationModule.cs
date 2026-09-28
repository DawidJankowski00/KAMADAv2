using Kamada.Modules.RouteOptimization.Application;
using Kamada.Modules.RouteOptimization.Application.Abstractions;
using Kamada.Modules.RouteOptimization.Contracts;
using Kamada.Modules.RouteOptimization.Domain.Solvers;
using Kamada.Modules.RouteOptimization.Infrastructure.GoogleMaps;
using Kamada.Modules.RouteOptimization.Infrastructure.Matrix;
using Kamada.Shared.Infrastructure.Http;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Kamada.Modules.RouteOptimization;

/// <summary>
/// Entry point of the RouteOptimization module (travel-time matrix + local TSP solvers + navigation links):
/// the only public type of the implementation assembly. Stateless – no database schema.
/// Exposes <see cref="IRoutePlanner"/> and <see cref="INavigationLinkBuilder"/> to other modules.
/// </summary>
public static class RouteOptimizationModule
{
    public const string Name = "RouteOptimization";

    private const string OsrmThrottleKey = "route-optimization:osrm-throttle";

    public static IServiceCollection AddRouteOptimizationModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<RouteOptimizationOptions>()
            .Bind(configuration.GetSection(RouteOptimizationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddOptions<GoogleMapsOptions>()
            .Bind(configuration.GetSection(GoogleMapsOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddMemoryCache();

        services.AddSingleton(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value;
            return new RouteSolverSelector(
                settings.BruteForceMaxStops,
                settings.HeldKarpMaxStops,
                new NearestNeighborTwoOptSolver(settings.HeuristicTimeLimit, sp.GetRequiredService<TimeProvider>()));
        });

        AddMatrixProviders(services, configuration);

        services.AddScoped<IRoutePlanner, RoutePlanner>();
        services.AddSingleton<INavigationLinkBuilder, GoogleMapsUrlBuilder>();

        services.AddHealthChecks()
            .AddCheck<TravelMatrixHealthCheck>("travel-matrix", failureStatus: HealthStatus.Degraded);

        return services;
    }

    public static IEndpointRouteBuilder MapRouteOptimizationEndpoints(this IEndpointRouteBuilder endpoints) => endpoints;

    private static void AddMatrixProviders(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<HaversineMatrixProvider>();

        var kind = configuration.GetValue(
            $"{RouteOptimizationOptions.SectionName}:{nameof(RouteOptimizationOptions.MatrixProvider)}",
            MatrixProviderKind.Osrm);

        switch (kind)
        {
            case MatrixProviderKind.Osrm:
                services.AddKeyedSingleton(OsrmThrottleKey, (sp, _) => new RequestThrottle(
                    sp.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value.Osrm.MinDelayBetweenRequests,
                    sp.GetRequiredService<TimeProvider>()));
                var osrm = services.AddHttpClient<OsrmMatrixProvider>((sp, http) =>
                {
                    var options = sp.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value.Osrm;
                    http.BaseAddress = options.BaseUrl;
                    http.DefaultRequestHeaders.UserAgent.ParseAdd(options.UserAgent);
                });
                osrm.AddResilienceHandler("osrm", ConfigureResilience);
                osrm.AddHttpMessageHandler(sp => new ThrottlingHandler(sp.GetRequiredKeyedService<RequestThrottle>(OsrmThrottleKey)));
                services.AddKeyedTransient<ITravelTimeMatrixProvider>(
                    TravelMatrixHealthCheck.PrimaryProviderKey, (sp, _) => sp.GetRequiredService<OsrmMatrixProvider>());
                break;

            case MatrixProviderKind.Google:
                services.AddHttpClient<GoogleRoutesMatrixProvider>((sp, http) =>
                        http.BaseAddress = sp.GetRequiredService<IOptions<RouteOptimizationOptions>>().Value.Google.BaseUrl)
                    .AddResilienceHandler("google-routes", ConfigureResilience);
                services.AddKeyedTransient<ITravelTimeMatrixProvider>(
                    TravelMatrixHealthCheck.PrimaryProviderKey, (sp, _) => sp.GetRequiredService<GoogleRoutesMatrixProvider>());
                break;

            default:
                services.AddKeyedTransient<ITravelTimeMatrixProvider>(
                    TravelMatrixHealthCheck.PrimaryProviderKey, (sp, _) => sp.GetRequiredService<HaversineMatrixProvider>());
                break;
        }

        // cache → (primary → fallback to the straight-line estimate)
        services.AddScoped<ITravelTimeMatrixProvider>(sp =>
        {
            var settings = sp.GetRequiredService<IOptions<RouteOptimizationOptions>>();
            var primary = sp.GetRequiredKeyedService<ITravelTimeMatrixProvider>(TravelMatrixHealthCheck.PrimaryProviderKey);
            var resilient = settings.Value.FallbackToHaversine && kind != MatrixProviderKind.Haversine
                ? new FallbackTravelMatrixProvider(
                    primary,
                    sp.GetRequiredService<HaversineMatrixProvider>(),
                    sp.GetRequiredService<ILogger<FallbackTravelMatrixProvider>>())
                : primary;
            return new CachingTravelMatrixProvider(resilient, sp.GetRequiredService<IMemoryCache>(), settings);
        });
    }

    /// <summary>Matrix requests run inside an admin's "optimize" click: fail fast, then fall back.</summary>
    private static void ConfigureResilience(ResiliencePipelineBuilder<HttpResponseMessage> pipeline) => pipeline
        .AddTimeout(TimeSpan.FromSeconds(15))
        .AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(500) })
        .AddTimeout(TimeSpan.FromSeconds(5));
}
