namespace Kamada.Web.Shared.Offline;

/// <summary>Key-value storage on the device that survives reloads (IndexedDB in the browser).</summary>
public interface ILocalStore
{
    /// <summary>False when the browser offers no persistent storage; the app then keeps the data in memory only.</summary>
    ValueTask<bool> IsAvailableAsync();

    ValueTask<T?> GetAsync<T>(LocalStoreName store, string key);

    ValueTask<IReadOnlyList<T>> GetAllAsync<T>(LocalStoreName store);

    ValueTask PutAsync<T>(LocalStoreName store, string key, T value);

    ValueTask DeleteAsync(LocalStoreName store, string key);

    /// <summary>Empties <paramref name="store"/>, or everything when it is null.</summary>
    ValueTask ClearAsync(LocalStoreName? store = null);
}
