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
    // Demo-only credentials for local development. Never use them outside seeded demo data.
    public const string AdminPassword = "Admin123!";
    public const string CourierPassword = "Kurier123!";
    public const string ManagerPassword = "Kierownik123!";
    public const string SellerPassword = "Sprzedawca123!";
    public const string DeveloperPassword = "Deweloper123!";

    public int Order => SeedOrder.Workforce;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Workers.AnyAsync(cancellationToken))
        {
            return;
        }

        var admin = Add("admin", "admin@kamada.test", AdminPassword, "Anna", "Nowak", "+48 600 100 200", WorkerRole.Admin);
        var couriers = new[]
        {
            Add("kurier1", "kurier1@kamada.test", CourierPassword, "Jan", "Kowalski", "+48 601 111 111", WorkerRole.Courier),
            Add("kurier2", "kurier2@kamada.test", CourierPassword, "Piotr", "Wiśniewski", "+48 602 222 222", WorkerRole.Courier),
            Add("kurier3", "kurier3@kamada.test", CourierPassword, "Katarzyna", "Wójcik", "+48 603 333 333", WorkerRole.Courier),
        };

        var seller = Add("sprzedawca1", "sprzedawca1@kamada.test", SellerPassword, "Ewa", "Lewandowska", "+48 605 555 555", WorkerRole.Seller);

        // A worker with every role, for switching roles in the phone app.
        Add("kierownik", "kierownik@kamada.test", ManagerPassword, "Marek", "Zieliński", "+48 604 444 444", WorkerRole.Admin, WorkerRole.Courier, WorkerRole.Seller);

        // An admin who also sees the hidden developer menu (assistant provider settings).
        Add("developer", "developer@kamada.test", DeveloperPassword, "Tomasz", "Kamiński", "+48 606 666 666", WorkerRole.Admin, WorkerRole.Developer);

        await db.SaveChangesAsync(cancellationToken);
        seedData.AdminId = admin.Id;
        seedData.SellerId = seller.Id;
        seedData.CourierIds = [.. couriers.Select(c => c.Id)];
    }

    private Worker Add(string username, string email, string password, string name, string surname, string phone, params WorkerRole[] roles)
    {
        var worker = Worker.Create(username, email, string.Empty, name, surname, phone, roles).Value;
        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, password));
        db.Workers.Add(worker);
        return worker;
    }
}
