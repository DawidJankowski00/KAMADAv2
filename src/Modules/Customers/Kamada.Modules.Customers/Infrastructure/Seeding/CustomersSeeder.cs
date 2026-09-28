using System.Globalization;
using System.Text;
using Bogus;
using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Modules.Customers.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Geography;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Infrastructure.Seeding;

/// <summary>
/// 25 demo clients in Bydgoszcz. Addresses are real buildings with coordinates stored here (geocoded once with
/// Nominatim when the seed was written), so startup never depends on a geocoding service. Names (from lists of
/// common Polish names), e-mails and phone numbers are picked by Bogus with a fixed seed. Edge cases: one address
/// without coordinates (it cannot be planned into a run until pinned), one billing address and one deactivated client.
/// </summary>
internal sealed class CustomersSeeder(CustomersDbContext db, DemoSeedData seedData, TimeProvider clock) : IDataSeeder
{
    private const string City = "Bydgoszcz";
    private const string Country = "Polska";
    private const int ClientCount = 25;

    private static readonly DemoAddress[] Addresses =
    [
        new("Dworcowa", "63", "85-009", 53.1302312, 17.9957081),
        new("Jagiellońska", "12", "85-068", 53.1234573, 18.0060323),
        new("Mostowa", "7", "85-110", 53.1228364, 18.0007473),
        new("Długa", "22", "85-034", 53.1211416, 17.9983480),
        new("Sienkiewicza", "30", "85-038", 53.1317625, 18.0021677),
        new("Grunwaldzka", "55", "85-203", 53.1293182, 17.9787801),
        new("Szubińska", "20", "85-312", 53.1159756, 17.9649497),
        new("Wojska Polskiego", "30", "85-800", 53.1041850, 18.0534724),
        new("Nakielska", "100", "85-385", 53.1321552, 17.9417850),
        new("Glinki", "80", "85-861", 53.1011124, 18.0310171),
        new("Łęczycka", "15", "85-737", 53.1224904, 18.0562710),
        new("Szarych Szeregów", "5", "85-829", 53.1098166, 18.0456634),
        new("Chodkiewicza", "40", "85-671", 53.1314429, 18.0254278),
        new("Cieszkowskiego", "10", "85-052", 53.1310142, 18.0068678),
        new("Czerkaska", "10", "85-647", 53.1402844, 18.0254756),
        new("Leśna", "5", "85-676", 53.1380389, 18.0173876),
        new("Ikara", "10", "85-314", 53.1138428, 17.9669495),
        new("Gdańska", "110", "85-022", 53.1351442, 18.0133235),
        new("Toruńska", "60", "85-026", 53.1190243, 18.0173586),
        new("Kamienna", "5", "85-726", 53.1324443, 18.0499136),
        new("Łużycka", "10", "85-096", 53.1227342, 18.0296187),
        new("Kołłątaja", "10", "85-080", 53.1278449, 18.0106259),
        new("Śniadeckich", "20", "85-055", 53.1295350, 18.0032823),
        new("Wyzwolenia", "10", "85-799", 53.1516217, 18.1718627),
        new("Szpitalna", "5", "85-823", 53.1057632, 18.0465326),

        // Second shipping addresses (e.g. a workplace) of the first clients.
        new("Dworcowa", "30", "85-054", 53.1276019, 17.9998639),
        new("Pomorska", "60", "85-046", 53.1322674, 18.0056023),
        new("Gdańska", "80", "85-071", 53.1329531, 18.0111499),
        new("Chrobrego", "10", "85-047", 53.1307846, 18.0028442),
        new("Racławicka", "10", "85-041", 53.1358183, 18.0030470),
    ];

    /// <summary>Real building that "failed" geocoding – the admin has to pin it on the map.</summary>
    private static readonly DemoAddress NotGeocoded = new("Kraszewskiego", "10a", "85-240", null, null);

    private static readonly string[] MailDomains = ["gmail.com", "wp.pl", "onet.pl", "o2.pl", "interia.pl"];

    // Common Polish names; Bogus' "pl" locale mixes rare first names with surnames of the wrong gender.
    private static readonly string[] FemaleNames =
        ["Anna", "Maria", "Katarzyna", "Małgorzata", "Agnieszka", "Barbara", "Ewa", "Magdalena", "Joanna", "Aleksandra", "Monika", "Zofia", "Julia", "Natalia", "Karolina", "Marta"];

    private static readonly string[] MaleNames =
        ["Piotr", "Krzysztof", "Andrzej", "Tomasz", "Paweł", "Michał", "Marcin", "Jakub", "Adam", "Łukasz", "Mateusz", "Kamil", "Grzegorz", "Wojciech", "Bartosz", "Rafał"];

    /// <summary>Masculine and feminine form; at least <see cref="ClientCount"/> distinct surnames.</summary>
    private static readonly (string Male, string Female)[] Surnames =
    [
        ("Kamiński", "Kamińska"), ("Lewandowski", "Lewandowska"), ("Szymański", "Szymańska"), ("Woźniak", "Woźniak"),
        ("Dąbrowski", "Dąbrowska"), ("Kozłowski", "Kozłowska"), ("Jankowski", "Jankowska"), ("Mazur", "Mazur"),
        ("Kwiatkowski", "Kwiatkowska"), ("Krawczyk", "Krawczyk"), ("Piotrowski", "Piotrowska"), ("Grabowski", "Grabowska"),
        ("Nowakowski", "Nowakowska"), ("Pawłowski", "Pawłowska"), ("Michalski", "Michalska"), ("Adamczyk", "Adamczyk"),
        ("Dudek", "Dudek"), ("Zając", "Zając"), ("Wieczorek", "Wieczorek"), ("Jabłoński", "Jabłońska"),
        ("Król", "Król"), ("Majewski", "Majewska"), ("Olszewski", "Olszewska"), ("Jaworski", "Jaworska"),
        ("Malinowski", "Malinowska"), ("Pietrzak", "Pietrzak"), ("Stępień", "Stępień"), ("Górski", "Górska"),
    ];

    public int Order => SeedOrder.Customers;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Clients.AnyAsync(cancellationToken))
        {
            return;
        }

        var faker = new Faker("pl") { Random = new Randomizer(2026) };
        var now = clock.GetUtcNow();
        var clients = new List<(Client Client, DateTimeOffset CreatedAt)>();
        var shipping = new List<DemoShippingAddress>();
        var extraAddresses = new Queue<DemoAddress>(Addresses.Skip(ClientCount));
        var surnames = new Queue<(string Male, string Female)>(faker.Random.Shuffle(Surnames));

        for (var i = 0; i < ClientCount; i++)
        {
            var female = i % 2 == 0;
            var name = faker.PickRandom(female ? FemaleNames : MaleNames);
            var (male, feminine) = surnames.Dequeue();
            var surname = female ? feminine : male;
            var username = Ascii($"{name}.{surname}");

            var client = Client.Create(
                username,
                $"{username}@{faker.PickRandom(MailDomains)}",
                name,
                surname,
                faker.Random.ReplaceNumbers("+48 5## ### ###"));

            // Every third client lives in a block of flats.
            var apartment = i % 3 == 1 ? faker.Random.Number(1, 60).ToString(CultureInfo.InvariantCulture) : null;
            shipping.Add(new DemoShippingAddress(client.Id, AddAddress(client, Addresses[i], AddressType.Shipping, apartment, now).Id));
            if (i < 5 && extraAddresses.TryDequeue(out var second))
            {
                shipping.Add(new DemoShippingAddress(client.Id, AddAddress(client, second, AddressType.Shipping, apartment: null, now).Id));
            }

            clients.Add((client, now.AddDays(-faker.Random.Number(20, 400)).AddMinutes(-faker.Random.Number(0, 600))));
        }

        // Edge cases (not used by demo orders).
        AddAddress(clients[5].Client, Addresses[5], AddressType.Billing, apartment: null, now);
        AddAddress(clients[7].Client, NotGeocoded, AddressType.Shipping, apartment: "4", now);
        var inactive = Client.Create("tomasz.nieaktywny", "tomasz.nieaktywny@wp.pl", "Tomasz", "Zieliński", "+48 511 000 111");
        AddAddress(inactive, Addresses[20], AddressType.Shipping, apartment: "12", now);
        inactive.Deactivate();
        clients.Add((inactive, now.AddDays(-500)));

        db.Clients.AddRange(clients.Select(c => c.Client));
        await db.SaveChangesAsync(cancellationToken);

        // created_at is stamped on save; spread the sign-up dates over the past year.
        foreach (var (client, createdAt) in clients)
        {
            await db.Clients.Where(c => c.Id == client.Id).ExecuteUpdateAsync(s => s.SetProperty(c => c.CreatedAt, createdAt), cancellationToken);
        }

        // The update changed xmin: later seeders in this scope must load fresh entities.
        db.ChangeTracker.Clear();

        seedData.ShippingAddresses = shipping;
    }

    private static Address AddAddress(Client client, DemoAddress demo, AddressType type, string? apartment, DateTimeOffset now)
    {
        var address = client.AddAddress(new AddressDetails(type, demo.Street, demo.BuildingNumber, apartment, City, demo.PostalCode, Country));
        if (demo.Latitude is { } latitude && demo.Longitude is { } longitude)
        {
            address.SetGeocodedLocation(new GeoPoint(latitude, longitude), now);
        }

        return address;
    }

    /// <summary>"Łucja.Wójcik" → "lucja.wojcik" (usernames and e-mails are ASCII).</summary>
    private static string Ascii(string text)
    {
        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Replace('ł', 'l').Replace('Ł', 'L').Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return builder.ToString();
    }

    private sealed record DemoAddress(string Street, string BuildingNumber, string PostalCode, double? Latitude, double? Longitude);
}
