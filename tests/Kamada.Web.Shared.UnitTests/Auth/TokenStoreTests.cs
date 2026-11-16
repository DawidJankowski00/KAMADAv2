using Kamada.Web.Shared.Platform;
using System.Text.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;

namespace Kamada.Web.Shared.UnitTests.Auth;

public sealed class TokenStoreTests
{
    private const string StorageKey = "kamada.new.session";

    private readonly FakeJsRuntime _js = new();
    private readonly TokenStore _store;

    public TokenStoreTests()
    {
        _store = new TokenStore(new BrowserSessionStorage(_js), new KamadaClientOptions
        {
            ApiBaseUrl = new Uri("http://api.test/"),
            AllowedRoles = [Roles.Courier],
            StorageKey = StorageKey,
        });
    }

    [Fact]
    public async Task GetAsync_NothingStored_ReturnsNull()
    {
        var session = await _store.GetAsync();

        session.ShouldBeNull();
        _js.LocalStorage.ShouldBeEmpty();
    }
}
