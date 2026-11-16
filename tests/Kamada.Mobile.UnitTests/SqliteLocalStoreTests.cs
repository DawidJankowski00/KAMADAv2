using Kamada.Mobile.Storage;
using Kamada.Web.Shared.Offline;
using Microsoft.Data.Sqlite;

namespace Kamada.Mobile.UnitTests;

public sealed class SqliteLocalStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"kamada-store-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Put_ThenGet_ReturnsTheValueWithEnumsAndCamelCase()
    {
        var store = new SqliteLocalStore(_path);

        await store.PutAsync(LocalStoreName.Outbox, "a", new Sample("Kawa", 2, LocalStoreName.Meta));

        (await store.GetAsync<Sample>(LocalStoreName.Outbox, "a")).ShouldBe(new Sample("Kawa", 2, LocalStoreName.Meta));
        (await store.GetAsync<Sample>(LocalStoreName.Lookup, "a")).ShouldBeNull();
    }

    [Fact]
    public async Task Put_SameKeyTwice_ReplacesTheValue()
    {
        var store = new SqliteLocalStore(_path);

        await store.PutAsync(LocalStoreName.Meta, "owner", "first");
        await store.PutAsync(LocalStoreName.Meta, "owner", "second");

        (await store.GetAsync<string>(LocalStoreName.Meta, "owner")).ShouldBe("second");
        (await store.GetAllAsync<string>(LocalStoreName.Meta)).ShouldBe(["second"]);
    }

    [Fact]
    public async Task GetAll_ReturnsOnlyThatStore_InKeyOrder()
    {
        var store = new SqliteLocalStore(_path);
        await store.PutAsync(LocalStoreName.Outbox, "2", "b");
        await store.PutAsync(LocalStoreName.Outbox, "1", "a");
        await store.PutAsync(LocalStoreName.Lookup, "1", "x");

        (await store.GetAllAsync<string>(LocalStoreName.Outbox)).ShouldBe(["a", "b"]);
    }

    [Fact]
    public async Task Delete_RemovesOneEntry()
    {
        var store = new SqliteLocalStore(_path);
        await store.PutAsync(LocalStoreName.Outbox, "1", "a");
        await store.PutAsync(LocalStoreName.Outbox, "2", "b");

        await store.DeleteAsync(LocalStoreName.Outbox, "1");

        (await store.GetAllAsync<string>(LocalStoreName.Outbox)).ShouldBe(["b"]);
    }

    [Fact]
    public async Task Clear_OneStoreOrEverything()
    {
        var store = new SqliteLocalStore(_path);
        await store.PutAsync(LocalStoreName.Outbox, "1", "a");
        await store.PutAsync(LocalStoreName.Lookup, "1", "x");

        await store.ClearAsync(LocalStoreName.Lookup);
        (await store.GetAllAsync<string>(LocalStoreName.Lookup)).ShouldBeEmpty();
        (await store.GetAllAsync<string>(LocalStoreName.Outbox)).ShouldBe(["a"]);

        await store.ClearAsync();
        (await store.GetAllAsync<string>(LocalStoreName.Outbox)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Data_SurvivesANewInstance_LikeAnAppRestart()
    {
        await new SqliteLocalStore(_path).PutAsync(LocalStoreName.Outbox, "1", "a");

        (await new SqliteLocalStore(_path).GetAsync<string>(LocalStoreName.Outbox, "1")).ShouldBe("a");
    }

    [Fact]
    public async Task IsAvailable_UnwritableLocation_IsFalseAndCallsAreHarmless()
    {
        var store = new SqliteLocalStore(Path.Combine(_path + "-missing-directory", "store.db"));

        (await store.IsAvailableAsync()).ShouldBeFalse();
        await store.PutAsync(LocalStoreName.Outbox, "1", "a");
        (await store.GetAllAsync<string>(LocalStoreName.Outbox)).ShouldBeEmpty();
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
        {
            File.Delete(file);
        }
    }

    private sealed record Sample(string Name, int Quantity, LocalStoreName Kind);
}
