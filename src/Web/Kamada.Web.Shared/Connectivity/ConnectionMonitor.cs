using Kamada.Web.Shared.Auth;

namespace Kamada.Web.Shared.Connectivity;

/// <summary>
/// Tells the app how usable the connection to the API is. Combines the browser's online/offline events
/// (<see cref="SetBrowserOnline"/>, wired by <see cref="BrowserNetworkEvents"/>) with a periodic ping of
/// <c>/health/ping</c>: its latency and the number of consecutive failures decide between
/// <see cref="ConnectionQuality.Good"/>, <see cref="ConnectionQuality.Poor"/> and <see cref="ConnectionQuality.Offline"/>.
/// </summary>
/// <remarks>
/// The browser's <c>navigator.onLine</c> only knows whether there is a network interface, not whether the server is
/// reachable, so "online" always waits for a successful ping. Pings are more frequent while the connection is not good,
/// so the app notices quickly when it is back.
/// </remarks>
public sealed class ConnectionMonitor : IDisposable
{
    public const string PingPath = "health/ping";

    /// <summary>A ping slower than this makes the connection <see cref="ConnectionQuality.Poor"/>.</summary>
    public static readonly TimeSpan SlowResponse = TimeSpan.FromSeconds(2);

    /// <summary>A ping without an answer after this long counts as a failure.</summary>
    public static readonly TimeSpan PingTimeout = TimeSpan.FromSeconds(8);

    public static readonly TimeSpan GoodPingInterval = TimeSpan.FromSeconds(30);

    public static readonly TimeSpan BadPingInterval = TimeSpan.FromSeconds(10);

    /// <summary>Consecutive failed pings after which the server counts as unreachable.</summary>
    public const int FailuresUntilOffline = 2;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _checkLock = new(1, 1);

    private ITimer? _timer;
    private bool _browserOnline = true;
    private int _consecutiveFailures;
    private bool _disposed;

    public ConnectionMonitor(IHttpClientFactory httpClientFactory, TimeProvider clock)
    {
        _httpClientFactory = httpClientFactory;
        _clock = clock;
    }

    /// <summary>Raised when <see cref="Quality"/> changes (possibly on a timer thread).</summary>
    public event Action<ConnectionQuality>? QualityChanged;

    /// <summary>Optimistic until the first ping: the app starts as if online and finds out within one ping.</summary>
    public ConnectionQuality Quality { get; private set; } = ConnectionQuality.Good;

    public TimeSpan? LastLatency { get; private set; }

    /// <summary>Last time the server answered a ping.</summary>
    public DateTimeOffset? LastContactAt { get; private set; }

    public bool IsStarted => _timer is not null;

    /// <summary>Pings right away and then periodically; calling it again does nothing.</summary>
    public void Start()
    {
        if (_timer is not null || _disposed)
        {
            return;
        }

        // Created stopped and started once assigned: the first ping reschedules the timer when it finishes.
        _timer = _clock.CreateTimer(_ => _ = CheckAsync(), null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
    }

    /// <summary>Browser <c>online</c>/<c>offline</c> event. Going offline is immediate; coming back is confirmed by a ping.</summary>
    public Task SetBrowserOnline(bool online)
    {
        _browserOnline = online;
        if (!online)
        {
            SetQuality(ConnectionQuality.Offline);
            return Task.CompletedTask;
        }

        return CheckAsync();
    }

    /// <summary>Pings the server now (e.g. before a sync); concurrent callers share one ping.</summary>
    public async Task<ConnectionQuality> CheckAsync(CancellationToken cancellationToken = default)
    {
        if (!await _checkLock.WaitAsync(0, cancellationToken))
        {
            // A ping is running: wait for it instead of sending another one.
            await _checkLock.WaitAsync(cancellationToken);
            _checkLock.Release();
            return Quality;
        }

        try
        {
            SetQuality(_browserOnline ? await PingAsync(cancellationToken) : ConnectionQuality.Offline);
            return Quality;
        }
        finally
        {
            _checkLock.Release();
            ScheduleNextPing();
        }
    }

    public void Dispose()
    {
        _disposed = true;
        _timer?.Dispose();
        _checkLock.Dispose();
    }

    private async Task<ConnectionQuality> PingAsync(CancellationToken cancellationToken)
    {
        using var timeout = new CancellationTokenSource(PingTimeout, _clock);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, cancellationToken);
        var started = _clock.GetTimestamp();
        try
        {
            var http = _httpClientFactory.CreateClient(AuthService.AnonymousClientName);
            using var response = await http.GetAsync(PingPath, HttpCompletionOption.ResponseHeadersRead, linked.Token);
            if (response.IsSuccessStatusCode)
            {
                var latency = _clock.GetElapsedTime(started);
                _consecutiveFailures = 0;
                LastLatency = latency;
                LastContactAt = _clock.GetUtcNow();
                return latency > SlowResponse ? ConnectionQuality.Poor : ConnectionQuality.Good;
            }
        }
        catch (HttpRequestException)
        {
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
        }

        // No answer, or the server answered that it cannot work (e.g. 503 without its database).
        _consecutiveFailures++;
        LastLatency = null;
        return _consecutiveFailures >= FailuresUntilOffline ? ConnectionQuality.Offline : ConnectionQuality.Poor;
    }

    private void ScheduleNextPing()
    {
        if (_disposed)
        {
            return;
        }

        _timer?.Change(Quality == ConnectionQuality.Good ? GoodPingInterval : BadPingInterval, Timeout.InfiniteTimeSpan);
    }

    private void SetQuality(ConnectionQuality quality)
    {
        if (quality == Quality)
        {
            return;
        }

        Quality = quality;
        QualityChanged?.Invoke(quality);
    }
}
