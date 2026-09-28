namespace Kamada.Shared.Infrastructure.Persistence;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";

    /// <summary>Apply EF Core migrations of every module on startup (dev, docker demo, tests).</summary>
    public bool ApplyMigrationsOnStartup { get; init; }

    /// <summary>Run <see cref="IDataSeeder"/>s after migrations (demo accounts and data).</summary>
    public bool SeedDemoData { get; init; }
}
