using System.Text.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Platform;

namespace Kamada.Web.Shared.Auth;

/// <summary>
/// Persists the token pair (<see cref="ISessionStorage"/>) so the session survives app restarts (the courier stays
/// signed in). Storage failures degrade to an in-memory session.
/// </summary>
public sealed class TokenStore(ISessionStorage storage, KamadaClientOptions options)
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
            var json = await storage.GetAsync(options.StorageKey);
            if (string.IsNullOrEmpty(json) && options.PreviousStorageKey is { } previousKey)
            {
                json = await MoveFromPreviousKeyAsync(previousKey);
            }

            _session = string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<TokenResponse>(json, KamadaJson.Options);

            // Sessions saved before workers had several roles carry a single "role"; such a user signs in again.
            if (_session?.User.Roles is null)
            {
                _session = null;
            }
        }
        catch (JsonException)
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
        await storage.SetAsync(options.StorageKey, JsonSerializer.Serialize(session, KamadaJson.Options));
    }

    public async ValueTask ClearAsync()
    {
        _session = null;
        _loaded = true;
        await storage.RemoveAsync(options.StorageKey);
    }

    /// <summary>Keeps users signed in when an app changes its storage key (e.g. the courier app became the phone app).</summary>
    private async ValueTask<string?> MoveFromPreviousKeyAsync(string previousKey)
    {
        var json = await storage.GetAsync(previousKey);
        if (!string.IsNullOrEmpty(json))
        {
            await storage.SetAsync(options.StorageKey, json);
            await storage.RemoveAsync(previousKey);
        }

        return json;
    }
}
