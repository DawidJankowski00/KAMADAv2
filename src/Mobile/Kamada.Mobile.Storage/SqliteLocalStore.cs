using System.Text.Json;
using Kamada.Web.Shared.Api;
using Kamada.Web.Shared.Offline;
using Microsoft.Data.Sqlite;

namespace Kamada.Mobile.Storage;

/// <summary>
/// <see cref="ILocalStore"/> in a SQLite file in the app's private data directory: one table of JSON values keyed by
/// store and key, the same shape as the browser's IndexedDB stores. Signing out clears every row (<see cref="ClearAsync"/>).
/// A connection per call (pooled by Microsoft.Data.Sqlite); WAL keeps reads and writes from blocking each other.
/// </summary>
public sealed class SqliteLocalStore(string databasePath) : ILocalStore
{
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = databasePath,
        Mode = SqliteOpenMode.ReadWriteCreate,
        Pooling = true,
    }.ToString();

    private readonly SemaphoreSlim _init = new(1, 1);
    private bool? _available;

    public async ValueTask<bool> IsAvailableAsync()
    {
        if (_available is { } known)
        {
            return known;
        }

        await _init.WaitAsync();
        try
        {
            if (_available is null)
            {
                try
                {
                    await using var connection = await OpenAsync();
                    await ExecuteAsync(connection, """
                        PRAGMA journal_mode = WAL;
                        CREATE TABLE IF NOT EXISTS entries (
                            store TEXT NOT NULL,
                            key   TEXT NOT NULL,
                            value TEXT NOT NULL,
                            PRIMARY KEY (store, key)
                        ) WITHOUT ROWID;
                        """);
                    _available = true;
                }
                catch (SqliteException)
                {
                    _available = false;
                }
            }

            return _available.Value;
        }
        finally
        {
            _init.Release();
        }
    }

    public async ValueTask<T?> GetAsync<T>(LocalStoreName store, string key)
    {
        if (!await IsAvailableAsync())
        {
            return default;
        }

        await using var connection = await OpenAsync();
        await using var command = Command(connection, "SELECT value FROM entries WHERE store = $store AND key = $key", store, key);
        return await command.ExecuteScalarAsync() is string json ? JsonSerializer.Deserialize<T>(json, KamadaJson.Options) : default;
    }

    public async ValueTask<IReadOnlyList<T>> GetAllAsync<T>(LocalStoreName store)
    {
        if (!await IsAvailableAsync())
        {
            return [];
        }

        await using var connection = await OpenAsync();
        await using var command = Command(connection, "SELECT value FROM entries WHERE store = $store ORDER BY key", store);
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<T>();
        while (await reader.ReadAsync())
        {
            values.Add(JsonSerializer.Deserialize<T>(reader.GetString(0), KamadaJson.Options)!);
        }

        return values;
    }

    public async ValueTask PutAsync<T>(LocalStoreName store, string key, T value)
    {
        if (!await IsAvailableAsync())
        {
            return;
        }

        await using var connection = await OpenAsync();
        await using var command = Command(connection, """
            INSERT INTO entries (store, key, value) VALUES ($store, $key, $value)
            ON CONFLICT (store, key) DO UPDATE SET value = excluded.value
            """, store, key);
        command.Parameters.AddWithValue("$value", JsonSerializer.Serialize(value, KamadaJson.Options));
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask DeleteAsync(LocalStoreName store, string key)
    {
        if (!await IsAvailableAsync())
        {
            return;
        }

        await using var connection = await OpenAsync();
        await using var command = Command(connection, "DELETE FROM entries WHERE store = $store AND key = $key", store, key);
        await command.ExecuteNonQueryAsync();
    }

    public async ValueTask ClearAsync(LocalStoreName? store = null)
    {
        if (!await IsAvailableAsync())
        {
            return;
        }

        await using var connection = await OpenAsync();
        await using var command = store is { } name
            ? Command(connection, "DELETE FROM entries WHERE store = $store", name)
            : Command(connection, "DELETE FROM entries");
        await command.ExecuteNonQueryAsync();
    }

    private async Task<SqliteConnection> OpenAsync()
    {
        var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static SqliteCommand Command(SqliteConnection connection, string sql, LocalStoreName? store = null, string? key = null)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        if (store is { } name)
        {
            command.Parameters.AddWithValue("$store", name.ToString());
        }

        if (key is not null)
        {
            command.Parameters.AddWithValue("$key", key);
        }

        return command;
    }
}
