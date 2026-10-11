using Microsoft.JSInterop;

namespace Kamada.Web.Shared.UnitTests.Auth;

/// <summary>In-memory <c>localStorage</c> for <see cref="Kamada.Web.Shared.Auth.TokenStore"/>.</summary>
internal sealed class FakeJsRuntime : IJSRuntime
{
    public Dictionary<string, string> LocalStorage { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        var key = (string)args![0]!;
        switch (identifier)
        {
            case "localStorage.getItem":
                return new((TValue)(object?)LocalStorage.GetValueOrDefault(key)!);
            case "localStorage.setItem":
                LocalStorage[key] = (string)args[1]!;
                break;
            case "localStorage.removeItem":
                LocalStorage.Remove(key);
                break;
            default:
                throw new NotSupportedException(identifier);
        }

        return new(default(TValue)!);
    }
}
