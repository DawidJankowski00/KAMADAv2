namespace Kamada.Web.Shared.Platform;

/// <summary>
/// Where the signed-in session (the token pair) is kept between app starts: <c>localStorage</c> in the browser, the
/// platform's secure storage in the native app. Best effort – a failing store behaves like an empty one, so the app
/// falls back to an in-memory session.
/// </summary>
public interface ISessionStorage
{
    ValueTask<string?> GetAsync(string key);

    ValueTask SetAsync(string key, string value);

    ValueTask RemoveAsync(string key);
}
