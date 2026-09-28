namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Populates a module's tables with demo data. Must be idempotent (skip when data already exists).
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower runs first; modules that others depend on (accounts, catalog) use lower values.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}
