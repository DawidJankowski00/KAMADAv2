using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Shared.Infrastructure.Geocoding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Testcontainers.PostgreSql;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the whole API against a throwaway PostgreSQL container. Shared by all tests in the assembly,
/// so tests must not depend on global row counts – each test creates the data it needs.
/// External services (geocoding, the language and speech models) are replaced with deterministic fakes.
/// </summary>
public sealed class KamadaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        // Start the host (and apply migrations) before any test runs. Otherwise a test building a second host with
        // WithWebHostBuilder can migrate the same database in parallel: EF's migration lock is held per migration
        // transaction, so with two migrations in a module both hosts may apply the second one.
        _ = Services;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // UseSetting (not ConfigureAppConfiguration) so values are visible while Program builds services.
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Kamada", _postgres.GetConnectionString());
        builder.UseSetting("Database:ApplyMigrationsOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "false");
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789");
        builder.UseSetting("RateLimiting:Login:PermitLimit", "100000");
        builder.UseSetting("Geocoding:Provider", "None");
        builder.UseSetting("RouteOptimization:MatrixProvider", "Haversine");
        builder.UseSetting("Assistant:Enabled", "true");
        builder.UseSetting("RateLimiting:Assistant:PermitLimit", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IGeocodingService, FakeGeocodingService>());
            services.Replace(ServiceDescriptor.Singleton<IOrderExtractor>(OrderExtractor));
            services.Replace(ServiceDescriptor.Singleton<ILanguageModelProbe, FakeLanguageModelProbe>());
            services.Replace(ServiceDescriptor.Singleton<ISpeechToText, FakeSpeechToText>());
        });
    }

    /// <summary>Shared by every host built from this factory; tests register extractions for their own note texts.</summary>
    internal FakeOrderExtractor OrderExtractor { get; } = new();

    /// <summary>
    /// A second host with demo seeding enabled, on its own (new) database in the same container, so seeding starts
    /// from an empty database regardless of what other tests created.
    /// </summary>
    public WebApplicationFactory<Program> WithDemoData(string database) =>
        WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Kamada", new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString()) { Database = database }.ConnectionString)
            .UseSetting("Database:SeedDemoData", "true"));

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
