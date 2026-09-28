using Npgsql;

namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// One physical connection per DI scope (HTTP request), shared by every module <c>DbContext</c>.
/// This is what lets <see cref="IUnitOfWork"/> run changes of several modules in one local transaction
/// without escalating to a distributed one.
/// </summary>
internal sealed class ScopedDbConnection(NpgsqlDataSource dataSource) : IAsyncDisposable
{
    public NpgsqlConnection Connection { get; } = dataSource.CreateConnection();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();
}
