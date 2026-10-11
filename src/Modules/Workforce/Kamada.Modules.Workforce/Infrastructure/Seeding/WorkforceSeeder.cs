using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Infrastructure.Seeding;

/// <summary>
/// Demo accounts documented in the README. Runs only when <c>Database:SeedDemoData</c> is enabled.
/// </summary>
internal sealed class WorkforceSeeder(WorkforceDbContext db, IPasswordHasher<Worker> passwordHasher, DemoSeedData seedData) : IDataSeeder
{
    public const string AdminPassword = "Admin123!";
    public const string CourierPassword = "Kurier123!";
    public const string ManagerPassword = "Kierownik123!";

    public int Order => SeedOrder.Workforce;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Workers.AnyAsync(cancellationToken))
        {
            return;
        }

        Add("admin", AdminPassword, "Anna", "Nowak", "+48 600 100 200", WorkerRole.Admin);
        var couriers = new[]
        {
            Add("kurier1", CourierPassword, "Jan", "Kowalski", "+48 601 111 111", WorkerRole.Courier),
            Add("kurier2", CourierPassword, "Piotr", "Wiśniewski", "+48 602 222 222", WorkerRole.Courier),
            Add("kurier3", CourierPassword, "Katarzyna", "Wójcik", "+48 603 333 333", WorkerRole.Courier),
        };

        // A worker with several roles, for switching roles in the phone app.
        Add("kierownik", ManagerPassword, "Marek", "Zieliński", "+48 604 444 444", WorkerRole.Admin, WorkerRole.Courier);

        await db.SaveChangesAsync(cancellationToken);
        seedData.CourierIds = [.. couriers.Select(c => c.Id)];
    }

    private Worker Add(string username, string password, string name, string surname, string phone, params WorkerRole[] roles)
    {
        var worker = Worker.Create(username, string.Empty, name, surname, phone, roles).Value;
        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, password));
        db.Workers.Add(worker);
        return worker;
    }
}
