namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Populates a module's tables with demo data. Must be idempotent (skip when data already exists).
/// All seeders run in one scope and pass generated ids on through <see cref="DemoSeedData"/>.
/// </summary>
public interface IDataSeeder
{
    /// <summary>Lower runs first (<see cref="SeedOrder"/>); modules that others depend on use lower values.</summary>
    int Order { get; }

    Task SeedAsync(CancellationToken cancellationToken);
}
