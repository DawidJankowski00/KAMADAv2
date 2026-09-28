using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Kamada.Shared.Infrastructure.Persistence;

public static class PersistenceServiceCollectionExtensions
{
    public const string ConnectionStringName = "Kamada";

    public const string MigrationsHistoryTable = "__ef_migrations_history";

    /// <summary>
    /// Registers the shared <see cref="NpgsqlDataSource"/> (connection pool) and persistence building blocks.
    /// </summary>
    public static IServiceCollection AddKamadaPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString(ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException($"Connection string '{ConnectionStringName}' is not configured.");
        }

        services.AddSingleton(_ => NpgsqlDataSource.Create(connectionString));
        services.AddScoped<ScopedDbConnection>();
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddSingleton<AuditingInterceptor>();
        services.AddOptions<DatabaseOptions>().Bind(configuration.GetSection(DatabaseOptions.SectionName));
        return services;
    }

    /// <summary>
    /// Registers a module <see cref="DbContext"/> mapped to its own PostgreSQL schema, with its own
    /// migrations history table, snake_case naming and auditing. All module contexts in a scope share
    /// one connection (<see cref="ScopedDbConnection"/>) so they can join one transaction.
    /// </summary>
    public static IServiceCollection AddModuleDbContext<TContext>(this IServiceCollection services, string schema)
        where TContext : DbContext
    {
        services.AddDbContext<TContext>((serviceProvider, options) => options
            .UseNpgsql(
                serviceProvider.GetRequiredService<ScopedDbConnection>().Connection,
                npgsql => npgsql.MigrationsHistoryTable(MigrationsHistoryTable, schema))
            .UseSnakeCaseNamingConvention()
            .AddInterceptors(serviceProvider.GetRequiredService<AuditingInterceptor>()));

        services.AddScoped<IModuleDatabase>(serviceProvider =>
            new ModuleDatabase<TContext>(serviceProvider.GetRequiredService<TContext>(), schema));

        return services;
    }
}
