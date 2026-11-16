using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Shared.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Optional connection string of the schema owner, used only to apply migrations. Without it the application's
    /// own connection (<c>ConnectionStrings:Kamada</c>) migrates, which then needs the right to create tables.
    /// </summary>
    public const string MigrationsConnectionStringName = "KamadaMigrations";

    /// <summary>
    /// Applies pending migrations of all modules and runs demo seeders, depending on <see cref="DatabaseOptions"/>.
    /// </summary>
    public static async Task InitializeDatabasesAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        var options = services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        if (options.ApplyMigrationsOnStartup)
        {
            var ownerConnectionString = services.GetRequiredService<IConfiguration>().GetConnectionString(MigrationsConnectionStringName);

            // A scope of its own: the module contexts get the owner's connection before they are used.
            await using var migrationScope = services.CreateAsyncScope();
            foreach (var database in migrationScope.ServiceProvider.GetServices<IModuleDatabase>())
            {
                logger.LogInformation("Applying migrations for schema {Schema}", database.Schema);
                await database.MigrateAsync(ownerConnectionString, cancellationToken);
            }
        }

        if (options.SeedDemoData)
        {
            await using var seedScope = services.CreateAsyncScope();
            foreach (var seeder in seedScope.ServiceProvider.GetServices<IDataSeeder>().OrderBy(s => s.Order))
            {
                logger.LogInformation("Running seeder {Seeder}", seeder.GetType().Name);
                await seeder.SeedAsync(cancellationToken);
            }
        }
    }
}
