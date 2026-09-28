using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.Timeout;

namespace Kamada.Shared.Infrastructure.Geocoding;

/// <summary>
/// Decorator: caches results (also "not found", for a shorter time) and turns provider failures into
/// <c>null</c> so saving an address never fails because a third-party service is down.
/// </summary>
internal sealed partial class CachingGeocodingService(
    IGeocodingService inner,
    IMemoryCache cache,
    IOptions<GeocodingOptions> options,
    ILogger<CachingGeocodingService> logger) : IGeocodingService
{
    private static readonly TimeSpan NotFoundCacheDuration = TimeSpan.FromMinutes(10);

    public async Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken)
    {
        var key = "geocode:" + request.ToQueryString().ToUpperInvariant();
        if (cache.TryGetValue(key, out GeocodingResult? cached))
        {
            return cached;
        }

        GeocodingResult? result;
        try
        {
            result = await inner.GeocodeAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutRejectedException or JsonException
                                       || (ex is TaskCanceledException && !cancellationToken.IsCancellationRequested))
        {
            LogProviderFailure(ex, request.ToQueryString());
            return null;
        }

        if (result is null)
        {
            LogNotFound(request.ToQueryString());
        }

        cache.Set(key, result, result is null ? NotFoundCacheDuration : options.Value.CacheDuration);
        return result;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Geocoding provider failed for '{Address}'")]
    private partial void LogProviderFailure(Exception exception, string address);

    [LoggerMessage(Level = LogLevel.Information, Message = "Address '{Address}' could not be geocoded")]
    private partial void LogNotFound(string address);
}

/// <summary>Used when geocoding is disabled: every address needs a manual pin.</summary>
internal sealed class DisabledGeocodingService : IGeocodingService
{
    public Task<GeocodingResult?> GeocodeAsync(GeocodingRequest request, CancellationToken cancellationToken) =>
        Task.FromResult<GeocodingResult?>(null);
}
