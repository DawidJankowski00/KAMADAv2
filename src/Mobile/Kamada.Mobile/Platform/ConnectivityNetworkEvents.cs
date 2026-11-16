using Kamada.Web.Shared.Connectivity;
using Kamada.Web.Shared.Platform;

namespace Kamada.Mobile.Platform;

/// <summary>
/// The platform's network state (MAUI <see cref="IConnectivity"/>) for <see cref="ConnectionMonitor"/>: losing the
/// network marks the connection offline at once, getting it back (or the app returning to the foreground) is confirmed
/// with a ping, as in the browser.
/// </summary>
public sealed class ConnectivityNetworkEvents(IConnectivity connectivity, ConnectionMonitor monitor) : INetworkEvents, IDisposable
{
    private bool _started;

    public async Task StartAsync()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        connectivity.ConnectivityChanged += OnConnectivityChanged;
        if (!HasNetwork(connectivity.NetworkAccess))
        {
            await monitor.SetBrowserOnline(false);
        }

        monitor.Start();
    }

    /// <summary>Called when the app comes back to the foreground (Android may have paused it for a long time).</summary>
    public Task OnResumedAsync() => _started ? monitor.SetBrowserOnline(HasNetwork(connectivity.NetworkAccess)) : Task.CompletedTask;

    public void Dispose() => connectivity.ConnectivityChanged -= OnConnectivityChanged;

    private void OnConnectivityChanged(object? sender, ConnectivityChangedEventArgs e) =>
        _ = monitor.SetBrowserOnline(HasNetwork(e.NetworkAccess));

    /// <summary>
    /// <see cref="NetworkAccess.ConstrainedInternet"/> (e.g. a captive portal) still counts: the ping decides whether the
    /// API answers.
    /// </summary>
    private static bool HasNetwork(NetworkAccess access) => access is NetworkAccess.Internet or NetworkAccess.ConstrainedInternet;
}
