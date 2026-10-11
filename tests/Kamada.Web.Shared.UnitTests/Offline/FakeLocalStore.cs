using System.Text.Json;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Offline;

namespace Kamada.Web.Shared.UnitTests.Offline;

/// <summary>In-memory <see cref="ILocalStore"/> that round-trips values through JSON, like IndexedDB does.</summary>
internal sealed class FakeLocalStore : ILocalStore
{
    public Dictionary<(LocalStoreName Store, string Key), string> Values { get; } = [];

    public ValueTask<bool> IsAvailableAsync() => ValueTask.FromResult(true);

    public ValueTask<T?> GetAsync<T>(LocalStoreName store, string key) =>
        ValueTask.FromResult(Values.TryGetValue((store, key), out var json) ? JsonSerializer.Deserialize<T>(json, KamadaJson.Options) : default);

    public ValueTask<IReadOnlyList<T>> GetAllAsync<T>(LocalStoreName store) =>
        ValueTask.FromResult<IReadOnlyList<T>>([.. Values.Where(v => v.Key.Store == store).Select(v => JsonSerializer.Deserialize<T>(v.Value, KamadaJson.Options)!)]);

    public ValueTask PutAsync<T>(LocalStoreName store, string key, T value)
    {
        Values[(store, key)] = JsonSerializer.Serialize(value, KamadaJson.Options);
        return ValueTask.CompletedTask;
    }

    public ValueTask DeleteAsync(LocalStoreName store, string key)
    {
        Values.Remove((store, key));
        return ValueTask.CompletedTask;
    }

    public ValueTask ClearAsync(LocalStoreName? store = null)
    {
        foreach (var key in Values.Keys.Where(k => store is null || k.Store == store).ToList())
        {
            Values.Remove(key);
        }

        return ValueTask.CompletedTask;
    }

    public bool Holds(LocalStoreName store) => Values.Keys.Any(k => k.Store == store);
}
