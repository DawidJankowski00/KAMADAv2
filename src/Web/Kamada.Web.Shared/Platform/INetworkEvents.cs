namespace Kamada.Web.Shared.Platform;

/// <summary>
/// Tells <c>ConnectionMonitor</c> when the device goes offline or online (and when the app comes back to the
/// foreground) and starts its pings: browser events in the web app, the platform's connectivity API in the native app.
/// Started once by the phone app's layout.
/// </summary>
public interface INetworkEvents
{
    Task StartAsync();
}
