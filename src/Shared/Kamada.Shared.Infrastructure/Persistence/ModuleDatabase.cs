using Microsoft.EntityFrameworkCore;

namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Lets the host migrate every module's <see cref="DbContext"/> without knowing the concrete types.
/// </summary>
public interface IModuleDatabase
{
    string Schema { get; }

    Task MigrateAsync(CancellationToken cancellationToken);
}

internal sealed class ModuleDatabase<TContext>(TContext context, string schema) : IModuleDatabase
    where TContext : DbContext
{
    public string Schema => schema;

    public Task MigrateAsync(CancellationToken cancellationToken) => context.Database.MigrateAsync(cancellationToken);
}
