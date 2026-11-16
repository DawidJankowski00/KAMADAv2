using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Lets the host (migrations) and <see cref="IUnitOfWork"/> (transactions) work with every module's
/// <see cref="DbContext"/> without knowing the concrete types.
/// </summary>
public interface IModuleDatabase
{
    string Schema { get; }

    DbContext Context { get; }

    /// <param name="ownerConnectionString">
    /// When set, migrations run on a connection of the schema owner instead of the application's own (which may only
    /// read and write data); the context must not have been used yet.
    /// </param>
    Task MigrateAsync(string? ownerConnectionString, CancellationToken cancellationToken);
}

internal sealed class ModuleDatabase<TContext>(TContext context, string schema) : IModuleDatabase
    where TContext : DbContext
{
    public string Schema => schema;

    public DbContext Context => context;

    public Task MigrateAsync(string? ownerConnectionString, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(ownerConnectionString))
        {
            context.Database.SetDbConnection(new NpgsqlConnection(ownerConnectionString), contextOwnsConnection: true);
        }

        return context.Database.MigrateAsync(cancellationToken);
    }
}
