using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Platform;

/// <summary>Recording through <c>voice-recorder.js</c> (AudioWorklet, resampled to 16 kHz in the browser).</summary>
public sealed class BrowserAudioRecorder(IJSRuntime js) : IAudioRecorder
{
    private IJSObjectReference? _module;
    private DotNetObjectReference<BrowserAudioRecorder>? _self;
    private IAudioRecorderListener? _listener;

    public async Task<string> StartAsync(TimeSpan maxDuration, IAudioRecorderListener listener)
    {
        _listener = listener;
        try
        {
            _module ??= await js.InvokeAsync<IJSObjectReference>("import", "./_content/Kamada.Web.Shared/voice-recorder.js");
            _self ??= DotNetObjectReference.Create(this);
            return await _module.InvokeAsync<string>("start", _self, maxDuration.TotalSeconds);
        }
        catch (JSException)
        {
            return "error";
        }
    }

    public async Task<byte[]?> StopAsync() =>
        _module is null ? null : await _module.InvokeAsync<byte[]?>("stop");

    public async Task DiscardAsync()
    {
        if (_module is not null)
        {
            await _module.InvokeVoidAsync("discard");
        }
    }

    [JSInvokable]
    public Task OnLevel(double level, double seconds) => _listener?.OnLevel(level, seconds) ?? Task.CompletedTask;

    [JSInvokable]
    public Task OnLimitReached() => _listener?.OnLimitReached() ?? Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.InvokeVoidAsync("discard");
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }

        _self?.Dispose();
    }
}
