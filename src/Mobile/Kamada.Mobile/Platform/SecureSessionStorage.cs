using Kamada.Web.Shared.Platform;

namespace Kamada.Mobile.Platform;

/// <summary>
/// The session in the platform's secure storage (Android: values encrypted with a key in the Android Keystore), so
/// the refresh token is not readable from the WebView's storage. A failing keystore (e.g. restored from a backup to
/// another device) is treated as an empty store: the worker signs in again.
/// </summary>
internal sealed class SecureSessionStorage(ISecureStorage secureStorage) : ISessionStorage
{
    public async ValueTask<string?> GetAsync(string key)
    {
        try
        {
            return await secureStorage.GetAsync(key);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            secureStorage.Remove(key);
            return null;
        }
    }

    public async ValueTask SetAsync(string key, string value)
    {
        try
        {
            await secureStorage.SetAsync(key, value);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // In-memory session only.
        }
    }

    public ValueTask RemoveAsync(string key)
    {
        secureStorage.Remove(key);
        return ValueTask.CompletedTask;
    }
}
