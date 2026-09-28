using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Kamada.IntegrationTests.Infrastructure;

internal sealed record TestWorker(Guid Id, string Username, string Password);

/// <summary>
/// Arranges worker accounts directly in the database (demo seeding is disabled in tests).
/// </summary>
internal static class TestWorkers
{
    public const string DefaultPassword = "Test1234!";

    public static async Task<TestWorker> CreateWorkerAsync(
        this KamadaApiFactory factory,
        WorkerRole role,
        bool isActive = true,
        string password = DefaultPassword)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<WorkforceDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Worker>>();

        var username = $"{role.ToString().ToLowerInvariant()}-{Guid.NewGuid():N}"[..24];
        var worker = Worker.Create(username, string.Empty, "Test", role.ToString(), "+48 600 000 000", role);
        worker.ChangePasswordHash(hasher.HashPassword(worker, password));
        if (!isActive)
        {
            worker.Deactivate(deactivatedBy: Guid.Empty);
        }

        db.Workers.Add(worker);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new TestWorker(worker.Id, username, password);
    }
}
