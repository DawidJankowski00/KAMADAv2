using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Kamada.Shared.Infrastructure.Geocoding;

public static class GeocodingServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IGeocodingService"/> for the configured provider, wrapped in a cache.
    /// </summary>
    public static IServiceCollection AddKamadaGeocoding(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<GeocodingOptions>()
            .Bind(configuration.GetSection(GeocodingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddMemoryCache();

        var provider = configuration.GetValue(
            $"{GeocodingOptions.SectionName}:{nameof(GeocodingOptions.Provider)}",
            GeocodingProvider.Nominatim);

        switch (provider)
        {
            case GeocodingProvider.Nominatim:
                services.AddSingleton(sp => new RequestThrottle(
                    sp.GetRequiredService<IOptions<GeocodingOptions>>().Value.Nominatim.MinDelayBetweenRequests,
                    sp.GetRequiredService<TimeProvider>()));
                services.AddTransient<ThrottlingHandler>();
                var nominatimClient = services.AddHttpClient<NominatimGeocodingService>((sp, http) =>
                {
                    var nominatim = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value.Nominatim;
                    http.BaseAddress = nominatim.BaseUrl;
                    http.DefaultRequestHeaders.UserAgent.ParseAdd(nominatim.UserAgent);
                });
                nominatimClient.AddResilienceHandler("nominatim", ConfigureResilience);
                nominatimClient.AddHttpMessageHandler<ThrottlingHandler>();
                services.AddScoped<IGeocodingService>(sp => Cached(sp, sp.GetRequiredService<NominatimGeocodingService>()));
                break;

            case GeocodingProvider.Google:
                services.AddHttpClient<GoogleGeocodingService>((sp, http) =>
                        http.BaseAddress = sp.GetRequiredService<IOptions<GeocodingOptions>>().Value.Google.BaseUrl)
                    .AddResilienceHandler("google-geocoding", ConfigureResilience);
                services.AddScoped<IGeocodingService>(sp => Cached(sp, sp.GetRequiredService<GoogleGeocodingService>()));
                break;

            default:
                services.AddSingleton<IGeocodingService, DisabledGeocodingService>();
                break;
        }

        return services;
    }

    private static CachingGeocodingService Cached(IServiceProvider sp, IGeocodingService inner) => new(
        inner,
        sp.GetRequiredService<IMemoryCache>(),
        sp.GetRequiredService<IOptions<GeocodingOptions>>(),
        sp.GetRequiredService<ILogger<CachingGeocodingService>>());

    /// <summary>Short timeouts: geocoding runs inside an admin's save request.</summary>
    private static void ConfigureResilience(ResiliencePipelineBuilder<HttpResponseMessage> pipeline) => pipeline
        .AddTimeout(TimeSpan.FromSeconds(10))
        .AddRetry(new HttpRetryStrategyOptions { MaxRetryAttempts = 2, Delay = TimeSpan.FromMilliseconds(500) })
        .AddTimeout(TimeSpan.FromSeconds(4));
}
