using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Platform;

/// <summary><c>localStorage</c> of the browser; storage failures (private mode, blocked storage) are ignored.</summary>
public sealed class BrowserSessionStorage(IJSRuntime js) : ISessionStorage
{
    public async ValueTask<string?> GetAsync(string key)
    {
        try
        {
            return await js.InvokeAsync<string?>("localStorage.getItem", key);
        }
        catch (JSException)
        {
            return null;
        }
    }

    public async ValueTask SetAsync(string key, string value)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", key, value);
        }
        catch (JSException)
        {
            // In-memory session only.
        }
    }

    public async ValueTask RemoveAsync(string key)
    {
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", key);
        }
        catch (JSException)
        {
            // Nothing persisted.
        }
    }
}
