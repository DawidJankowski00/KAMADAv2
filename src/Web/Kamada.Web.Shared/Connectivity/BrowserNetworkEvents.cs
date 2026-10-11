using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Connectivity;

/// <summary>
/// Forwards the browser's <c>online</c>/<c>offline</c> events (and the app becoming visible again) to
/// <see cref="ConnectionMonitor"/> and starts its pings. Started once by the app's layout.
/// </summary>
public sealed class BrowserNetworkEvents(IJSRuntime js, ConnectionMonitor monitor) : IAsyncDisposable
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserNetworkEvents>? _self;

    public async Task StartAsync()
    {
        if (_module is not null)
        {
            return;
        }

        try
        {
            _module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Kamada.Web.Shared/connection-monitor.js");
            _self = DotNetObjectReference.Create(this);
            var online = await _module.InvokeAsync<bool>("start", _self);
            if (!online)
            {
                await monitor.SetBrowserOnline(false);
            }
        }
        catch (JSException)
        {
            // Without the events the pings alone decide.
        }

        monitor.Start();
    }

    [JSInvokable]
    public Task OnBrowserOnlineChanged(bool online) => monitor.SetBrowserOnline(online);

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("stop");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _self?.Dispose();
    }
}
