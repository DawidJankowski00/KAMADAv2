using System.Net;
using Kamada.Web.Shared.Connectivity;
using Microsoft.Extensions.Time.Testing;

namespace Kamada.Web.Shared.UnitTests.Connectivity;

public sealed class ConnectionMonitorTests : IDisposable
{
    private readonly FakeTimeProvider _clock = new(DateTimeOffset.Parse("2026-10-11T08:00:00Z"));
    private readonly PingServer _server;
    private readonly ConnectionMonitor _monitor;
    private readonly List<ConnectionQuality> _changes = [];

    public ConnectionMonitorTests()
    {
        _server = new PingServer(_clock);
        _monitor = new ConnectionMonitor(_server, _clock);
        _monitor.QualityChanged += _changes.Add;
    }

    [Fact]
    public async Task CheckAsync_FastAnswer_IsGood()
    {
        _server.Latency = TimeSpan.FromMilliseconds(150);

        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Good);
        _monitor.LastLatency.ShouldBe(TimeSpan.FromMilliseconds(150));
        _monitor.LastContactAt.ShouldBe(_clock.GetUtcNow());
        _server.RequestedPaths.ShouldBe(["/health/ping"]);
    }

    [Fact]
    public async Task CheckAsync_SlowAnswer_IsPoor()
    {
        _server.Latency = ConnectionMonitor.SlowResponse + TimeSpan.FromMilliseconds(500);

        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Poor);
        _changes.ShouldBe([ConnectionQuality.Poor]);
    }

    [Fact]
    public async Task CheckAsync_OneFailedPing_IsPoor()
    {
        _server.Unreachable = true;

        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Poor);
        _monitor.LastLatency.ShouldBeNull();
    }

    [Fact]
    public async Task CheckAsync_ConsecutiveFailedPings_IsOffline()
    {
        _server.Unreachable = true;

        await _monitor.CheckAsync(TestContext.Current.CancellationToken);
        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Offline);
        _changes.ShouldBe([ConnectionQuality.Poor, ConnectionQuality.Offline]);
    }

    [Fact]
    public async Task CheckAsync_ServerUnavailable_CountsAsFailure()
    {
        _server.Status = HttpStatusCode.ServiceUnavailable;

        await _monitor.CheckAsync(TestContext.Current.CancellationToken);
        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Offline);
    }

    [Fact]
    public async Task CheckAsync_AnswerAfterFailures_IsGoodAgain()
    {
        _server.Unreachable = true;
        await _monitor.CheckAsync(TestContext.Current.CancellationToken);
        await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        _server.Unreachable = false;
        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Good);
        _changes.ShouldBe([ConnectionQuality.Poor, ConnectionQuality.Offline, ConnectionQuality.Good]);
    }

    [Fact]
    public async Task SetBrowserOnline_False_IsOfflineWithoutPing()
    {
        await _monitor.SetBrowserOnline(false);

        _monitor.Quality.ShouldBe(ConnectionQuality.Offline);
        _server.RequestedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task CheckAsync_BrowserOffline_StaysOfflineWithoutPing()
    {
        await _monitor.SetBrowserOnline(false);

        var quality = await _monitor.CheckAsync(TestContext.Current.CancellationToken);

        quality.ShouldBe(ConnectionQuality.Offline);
        _server.RequestedPaths.ShouldBeEmpty();
    }

    [Fact]
    public async Task SetBrowserOnline_BackOnline_ConfirmsWithPing()
    {
        await _monitor.SetBrowserOnline(false);

        await _monitor.SetBrowserOnline(true);

        _monitor.Quality.ShouldBe(ConnectionQuality.Good);
        _server.RequestedPaths.Count.ShouldBe(1);
    }

    [Fact]
    public void Start_PingsRightAwayThenEveryGoodInterval()
    {
        _monitor.Start();
        _server.RequestedPaths.Count.ShouldBe(1);

        _clock.Advance(ConnectionMonitor.GoodPingInterval - TimeSpan.FromSeconds(1));
        _server.RequestedPaths.Count.ShouldBe(1);

        _clock.Advance(TimeSpan.FromSeconds(1));
        _server.RequestedPaths.Count.ShouldBe(2);
    }

    [Fact]
    public void Start_ConnectionNotGood_PingsMoreOften()
    {
        _server.Unreachable = true;

        _monitor.Start();
        _clock.Advance(ConnectionMonitor.BadPingInterval);

        _server.RequestedPaths.Count.ShouldBe(2);
        _monitor.Quality.ShouldBe(ConnectionQuality.Offline);
    }

    public void Dispose()
    {
        _monitor.Dispose();
        _server.Dispose();
    }

    /// <summary>Answers <c>/health/ping</c> synchronously, moving the fake clock forward by <see cref="Latency"/>.</summary>
    private sealed class PingServer(FakeTimeProvider clock) : HttpMessageHandler, IHttpClientFactory
    {
        public TimeSpan Latency { get; set; } = TimeSpan.FromMilliseconds(100);

        public bool Unreachable { get; set; }

        public HttpStatusCode Status { get; set; } = HttpStatusCode.OK;

        public List<string> RequestedPaths { get; } = [];

        public HttpClient CreateClient(string name) => new(this, disposeHandler: false) { BaseAddress = new Uri("http://api.test/") };

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestedPaths.Add(request.RequestUri!.AbsolutePath);
            if (Unreachable)
            {
                throw new HttpRequestException("Connection refused");
            }

            clock.Advance(Latency);
            return Task.FromResult(new HttpResponseMessage(Status));
        }
    }
}
