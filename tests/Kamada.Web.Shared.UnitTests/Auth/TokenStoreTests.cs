using System.Text.Json;
using Kamada.Shared.Contracts.Auth;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Auth;

namespace Kamada.Web.Shared.UnitTests.Auth;

public sealed class TokenStoreTests
{
    private const string StorageKey = "kamada.new.session";
    private const string PreviousStorageKey = "kamada.old.session";

    private readonly FakeJsRuntime _js = new();
    private readonly TokenStore _store;

    public TokenStoreTests()
    {
        _store = new TokenStore(_js, new KamadaClientOptions
        {
            ApiBaseUrl = new Uri("http://api.test/"),
            AllowedRoles = [Roles.Courier],
            StorageKey = StorageKey,
            PreviousStorageKey = PreviousStorageKey,
        });
    }

    [Fact]
    public async Task GetAsync_SessionUnderPreviousKey_MovesItToTheCurrentKey()
    {
        var tokens = StubApi.Tokens(Roles.Courier);
        _js.LocalStorage[PreviousStorageKey] = JsonSerializer.Serialize(tokens, KamadaJson.Options);

        var session = await _store.GetAsync();

        session.ShouldNotBeNull();
        session.AccessToken.ShouldBe(tokens.AccessToken);
        _js.LocalStorage.ShouldContainKey(StorageKey);
        _js.LocalStorage.ShouldNotContainKey(PreviousStorageKey);
    }

    [Fact]
    public async Task GetAsync_SessionUnderBothKeys_UsesTheCurrentKey()
    {
        var current = StubApi.Tokens(Roles.Courier);
        _js.LocalStorage[StorageKey] = JsonSerializer.Serialize(current, KamadaJson.Options);
        _js.LocalStorage[PreviousStorageKey] = JsonSerializer.Serialize(StubApi.Tokens(Roles.Courier), KamadaJson.Options);

        var session = await _store.GetAsync();

        session!.AccessToken.ShouldBe(current.AccessToken);
    }

    [Fact]
    public async Task GetAsync_NothingStored_ReturnsNull()
    {
        var session = await _store.GetAsync();

        session.ShouldBeNull();
        _js.LocalStorage.ShouldBeEmpty();
    }
}
