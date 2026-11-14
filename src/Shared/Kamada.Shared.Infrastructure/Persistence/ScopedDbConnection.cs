using Npgsql;

namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// One physical connection per DI scope (HTTP request), shared by every module <c>DbContext</c>.
/// This is what lets <see cref="IUnitOfWork"/> run changes of several modules in one local transaction
/// without escalating to a distributed one.
/// </summary>
/// <remarks>
/// Also disposable synchronously: some framework code creates and disposes scopes without <c>await</c> (the Data
/// Protection key repository reads the key ring that way), and a scope holding an async-only service throws then.
/// </remarks>
internal sealed class ScopedDbConnection(NpgsqlDataSource dataSource) : IAsyncDisposable, IDisposable
{
    public NpgsqlConnection Connection { get; } = dataSource.CreateConnection();

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    public void Dispose() => Connection.Dispose();
}
