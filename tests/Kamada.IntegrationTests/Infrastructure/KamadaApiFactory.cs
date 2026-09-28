using Kamada.Shared.Infrastructure.Geocoding;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Kamada.IntegrationTests.Infrastructure;

/// <summary>
/// Boots the whole API against a throwaway PostgreSQL container. Shared by all tests in the assembly,
/// so tests must not depend on global row counts – each test creates the data it needs.
/// External services (geocoding) are replaced with deterministic fakes.
/// </summary>
public sealed class KamadaApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public async ValueTask InitializeAsync() => await _postgres.StartAsync();

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

        builder.ConfigureTestServices(services =>
            services.Replace(ServiceDescriptor.Singleton<IGeocodingService, FakeGeocodingService>()));
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
