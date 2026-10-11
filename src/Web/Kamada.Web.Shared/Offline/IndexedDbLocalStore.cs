using System.Text.Json;
using Kamada.Web.Shared.Api;
using Microsoft.JSInterop;

namespace Kamada.Web.Shared.Offline;

/// <summary>
/// <see cref="ILocalStore"/> over IndexedDB (<c>local-store.js</c>), one database per app. Values are serialized here with
/// <see cref="KamadaJson.Options"/>, the same options as the API calls. When IndexedDB is missing or fails, the store keeps
/// working in memory until the page is reloaded.
/// </summary>
public sealed class IndexedDbLocalStore(IJSRuntime js, KamadaClientOptions options) : ILocalStore, IAsyncDisposable
{
    private readonly Dictionary<(LocalStoreName, string), string> _memory = [];
    private IJSObjectReference? _module;
    private bool? _available;

    private string DatabaseName => options.StorageKey;

    public async ValueTask<bool> IsAvailableAsync()
    {
        if (_available is null)
        {
            try
            {
                _module = await js.InvokeAsync<IJSObjectReference>("import", "./_content/Kamada.Web.Shared/local-store.js");
                _available = await _module.InvokeAsync<bool>("isSupported");
                if (_available == true)
                {
                    // Queued orders must not be evicted under storage pressure; the browser may say no.
                    await _module.InvokeAsync<bool>("persist");
                }
            }
            catch (JSException)
            {
                _available = false;
            }
        }

        return _available.Value;
    }

    public async ValueTask<T?> GetAsync<T>(LocalStoreName store, string key)
    {
        var json = await TryAsync(
            module => module.InvokeAsync<string?>("get", DatabaseName, Name(store), key),
            () => _memory.GetValueOrDefault((store, key)));
        return json is null ? default : JsonSerializer.Deserialize<T>(json, KamadaJson.Options);
    }

    public async ValueTask<IReadOnlyList<T>> GetAllAsync<T>(LocalStoreName store)
    {
        var values = await TryAsync(
            module => module.InvokeAsync<string[]>("getAll", DatabaseName, Name(store)),
            () => [.. _memory.Where(e => e.Key.Item1 == store).Select(e => e.Value)]);
        return [.. values.Select(json => JsonSerializer.Deserialize<T>(json, KamadaJson.Options)!)];
    }

    public async ValueTask PutAsync<T>(LocalStoreName store, string key, T value)
    {
        var json = JsonSerializer.Serialize(value, KamadaJson.Options);
        await TryAsync<object?>(
            async module =>
            {
                await module.InvokeVoidAsync("put", DatabaseName, Name(store), key, json);
                return null;
            },
            () =>
            {
                _memory[(store, key)] = json;
                return null;
            });
    }

    public async ValueTask DeleteAsync(LocalStoreName store, string key) =>
        await TryAsync<object?>(
            async module =>
            {
                await module.InvokeVoidAsync("remove", DatabaseName, Name(store), key);
                return null;
            },
            () =>
            {
                _memory.Remove((store, key));
                return null;
            });

    public async ValueTask ClearAsync(LocalStoreName? store = null)
    {
        foreach (var key in _memory.Keys.Where(k => store is null || k.Item1 == store).ToList())
        {
            _memory.Remove(key);
        }

        await TryAsync<object?>(
            async module =>
            {
                await module.InvokeVoidAsync("clear", DatabaseName, store is null ? null : Name(store.Value));
                return null;
            },
            () => null);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module is not null)
        {
            try
            {
                await _module.DisposeAsync();
            }
            catch (JSDisconnectedException)
            {
            }
        }
    }

    private static string Name(LocalStoreName store) => store switch
    {
        LocalStoreName.Lookup => "lookup",
        LocalStoreName.Outbox => "outbox",
        LocalStoreName.Meta => "meta",
        _ => throw new ArgumentOutOfRangeException(nameof(store), store, null),
    };

    private async ValueTask<TResult> TryAsync<TResult>(Func<IJSObjectReference, ValueTask<TResult>> indexedDb, Func<TResult> inMemory)
    {
        if (await IsAvailableAsync() && _module is not null)
        {
            try
            {
                return await indexedDb(_module);
            }
            catch (JSException)
            {
                // E.g. quota exceeded or the database was deleted by the user: continue in memory.
                _available = false;
            }
        }

        return inMemory();
    }
}
