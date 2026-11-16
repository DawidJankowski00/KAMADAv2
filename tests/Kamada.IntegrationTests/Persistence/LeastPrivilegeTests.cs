using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Hosting;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Persistence;

/// <summary>
/// deploy/postgres/app-role.sql on a fresh database: migrations run as the owner, the API as a role that may only read
/// and write data.
/// </summary>
public sealed class LeastPrivilegeTests(KamadaApiFactory factory)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ApiWithTheAppRole_WorksButCannotChangeTheSchema()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var database = $"kamada_least_{suffix}";
        var appUser = $"kamada_app_{suffix}";
        const string AppPassword = "app-role-test-password";
        await CreateDatabaseWithAppRoleAsync(database, appUser, AppPassword);
        var appConnection = new NpgsqlConnectionStringBuilder(factory.AdminConnectionString(database)) { Username = appUser, Password = AppPassword }.ConnectionString;

        await using (var host = factory.WithWebHostBuilder(builder => builder
            .UseSetting("ConnectionStrings:Kamada", appConnection)
            .UseSetting("ConnectionStrings:KamadaMigrations", factory.AdminConnectionString(database))))
        {
            // The host migrated as the owner on start; the app role now writes data (a worker, a session) and reads it.
            var client = host.CreateClient();
            var worker = await CreateWorkerAsAppRoleAsync(host);
            (await client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password)))
                .StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using var app = new NpgsqlConnection(appConnection);
        await app.OpenAsync(Ct);
        await ShouldBeDeniedAsync(app, "CREATE TABLE workforce.intruder (id int)");
        await ShouldBeDeniedAsync(app, "DROP TABLE workforce.workers");
        await ShouldBeDeniedAsync(app, "ALTER TABLE workforce.workers ADD COLUMN extra int");
        await ShouldBeDeniedAsync(app, "TRUNCATE workforce.refresh_tokens");
        await ShouldBeDeniedAsync(app, "CREATE SCHEMA intruder");
    }

    private async Task CreateDatabaseWithAppRoleAsync(string database, string appUser, string appPassword)
    {
        await using (var admin = new NpgsqlConnection(factory.AdminConnectionString()))
        {
            await admin.OpenAsync(Ct);
            await using var create = new NpgsqlCommand($"CREATE DATABASE {database}", admin);
            await create.ExecuteNonQueryAsync(Ct);
        }

        var owner = new NpgsqlConnectionStringBuilder(factory.AdminConnectionString()).Username!;
        var script = (await File.ReadAllTextAsync(Path.Combine(RepositoryRoot(), "deploy", "postgres", "app-role.sql"), Ct))
            .Replace(":\"app_user\"", $"\"{appUser}\"", StringComparison.Ordinal)
            .Replace(":'app_password'", $"'{appPassword}'", StringComparison.Ordinal)
            .Replace(":\"owner\"", $"\"{owner}\"", StringComparison.Ordinal)
            .Replace(":\"db\"", $"\"{database}\"", StringComparison.Ordinal);

        await using var connection = new NpgsqlConnection(factory.AdminConnectionString(database));
        await connection.OpenAsync(Ct);
        await using var command = new NpgsqlCommand(script, connection);
        await command.ExecuteNonQueryAsync(Ct);
    }

    private static async Task<TestWorker> CreateWorkerAsAppRoleAsync(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> host)
    {
        // Arranged through the host's own services, so the insert runs on the app role's connection.
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<Kamada.Modules.Workforce.Infrastructure.Persistence.WorkforceDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<Microsoft.AspNetCore.Identity.IPasswordHasher<Kamada.Modules.Workforce.Domain.Workers.Worker>>();
        var username = $"least-{Guid.NewGuid():N}"[..20];
        var worker = Kamada.Modules.Workforce.Domain.Workers.Worker.Create(username, $"{username}@kamada.test", string.Empty, "Test", "Least", "+48 600 000 000", [WorkerRole.Courier]).Value;
        worker.ChangePasswordHash(hasher.HashPassword(worker, TestWorkers.DefaultPassword));
        db.Workers.Add(worker);
        await db.SaveChangesAsync(Ct);
        return new TestWorker(worker.Id, username, TestWorkers.DefaultPassword);
    }

    private static async Task ShouldBeDeniedAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        var error = await Should.ThrowAsync<PostgresException>(() => command.ExecuteNonQueryAsync(Ct));
        error.SqlState.ShouldBeOneOf(PostgresErrorCodes.InsufficientPrivilege, "42501");
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Kamada.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Kamada.slnx not found above the test output.");
    }
}
