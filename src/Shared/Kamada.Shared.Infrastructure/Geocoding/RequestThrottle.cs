namespace Kamada.Shared.Infrastructure.Geocoding;

/// <summary>
/// Process-wide gate that spaces outgoing requests by a minimum delay (e.g. Nominatim: 1 request/second).
/// Registered as a singleton; consumed by <see cref="ThrottlingHandler"/>.
/// </summary>
public sealed class RequestThrottle(TimeSpan minDelay, TimeProvider clock) : IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset _lastRequest = DateTimeOffset.MinValue;

    public async Task WaitAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var wait = _lastRequest + minDelay - clock.GetUtcNow();
            if (wait > TimeSpan.Zero)
            {
                await Task.Delay(wait, clock, cancellationToken);
            }

            _lastRequest = clock.GetUtcNow();
        }
        finally
        {
            _gate.Release();
        }
    }

    public void Dispose() => _gate.Dispose();
}

/// <summary>
/// Placed inside the resilience pipeline so that every attempt (including retries) is throttled.
/// </summary>
internal sealed class ThrottlingHandler(RequestThrottle throttle) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await throttle.WaitAsync(cancellationToken);
        return await base.SendAsync(request, cancellationToken);
    }
}
