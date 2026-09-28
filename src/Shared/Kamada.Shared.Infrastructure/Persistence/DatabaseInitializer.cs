using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Shared.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations of all modules and runs demo seeders, depending on <see cref="DatabaseOptions"/>.
    /// </summary>
    public static async Task InitializeDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var options = provider.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.ApplyMigrationsOnStartup)
        {
            foreach (var database in provider.GetServices<IModuleDatabase>())
            {
                logger.LogInformation("Applying migrations for schema {Schema}", database.Schema);
                await database.MigrateAsync(cancellationToken);
            }
        }

        if (options.SeedDemoData)
        {
            foreach (var seeder in provider.GetServices<IDataSeeder>().OrderBy(s => s.Order))
            {
                logger.LogInformation("Running seeder {Seeder}", seeder.GetType().Name);
                await seeder.SeedAsync(cancellationToken);
            }
        }
    }
}
