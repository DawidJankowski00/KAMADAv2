using System.Text.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Persists the token pair in <c>localStorage</c> so the session survives page reloads (the courier stays signed in).
/// Storage failures (private mode, blocked storage) degrade to an in-memory session.
/// </summary>
public sealed class TokenStore(IJSRuntime js, KamadaClientOptions options)
{
    private TokenResponse? _session;
    private bool _loaded;

    public async ValueTask<TokenResponse?> GetAsync()
    {
        if (_loaded)
        {
            return _session;
        }

        try
        {
            var json = await js.InvokeAsync<string?>("localStorage.getItem", options.StorageKey);
            _session = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TokenResponse>(json, KamadaJson.Options);
        }
        catch (Exception ex) when (ex is JSException or JsonException)
        {
            _session = null;
        }

        _loaded = true;
        return _session;
    }

    public async ValueTask SetAsync(TokenResponse session)
    {
        _session = session;
        _loaded = true;
        try
        {
            await js.InvokeVoidAsync("localStorage.setItem", options.StorageKey, JsonSerializer.Serialize(session, KamadaJson.Options));
        }
        catch (JSException)
        {
            // In-memory session only.
        }
    }

    public async ValueTask ClearAsync()
    {
        _session = null;
        _loaded = true;
        try
        {
            await js.InvokeVoidAsync("localStorage.removeItem", options.StorageKey);
        }
        catch (JSException)
        {
            // Nothing persisted.
        }
    }
}
