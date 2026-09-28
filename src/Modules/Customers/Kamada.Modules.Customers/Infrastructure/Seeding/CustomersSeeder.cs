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
/// 25 demo clients in the Tricity (Gdańsk, Gdynia, Sopot) and Szczecin. Addresses are real buildings with coordinates stored here (geocoded once with
/// Nominatim when the seed was written), so startup never depends on a geocoding service. Names (from lists of
/// common Polish names), e-mails and phone numbers are picked by Bogus with a fixed seed. Edge cases: one address
/// without coordinates (it cannot be planned into a run until pinned), one billing address and one deactivated client.
/// </summary>
internal sealed class CustomersSeeder(CustomersDbContext db, DemoSeedData seedData, TimeProvider clock) : IDataSeeder
{
    private const string Country = "Polska";
    private const int ClientCount = 25;

    /// <summary>Home address of client <c>i</c>: the first 13 clients live in the Tricity, the rest in Szczecin.</summary>
    private static readonly DemoAddress[] Addresses =
    [
        new("Gdańsk", "Długa", "45", "80-827", 54.3485048, 18.6526611),
        new("Gdańsk", "Grunwaldzka", "100", "80-244", 54.3780656, 18.6060457),
        new("Gdańsk", "Słowackiego", "50", "80-257", 54.3794365, 18.5843039),
        new("Gdańsk", "Kartuska", "100", "80-104", 54.3487270, 18.6211358),
        new("Gdańsk", "Piastowska", "50", "80-332", 54.4137710, 18.5789080),
        new("Gdańsk", "Obrońców Wybrzeża", "5", "80-398", 54.4105500, 18.5959260),
        new("Gdynia", "Świętojańska", "50", "81-393", 54.5167075, 18.5410578),
        new("Gdynia", "Starowiejska", "30", "81-356", 54.5214297, 18.5374622),
        new("Sopot", "Bohaterów Monte Cassino", "30", "81-759", 54.4435621, 18.5654380),
        new("Gdańsk", "Szeroka", "50", "80-835", 54.3507681, 18.6555367),
        new("Gdańsk", "Pilotów", "10", "80-460", 54.3918235, 18.5915741),
        new("Gdynia", "Władysława IV", "40", "81-397", 54.5171151, 18.5382378),
        new("Sopot", "Niepodległości", "800", "81-805", 54.4440851, 18.5591834),

        new("Szczecin", "Wojska Polskiego", "50", "70-473", 53.4312619, 14.5412203),
        new("Szczecin", "Jagiellońska", "20", "70-363", 53.4325477, 14.5379823),
        new("Szczecin", "Niepodległości", "30", "70-412", 53.4275508, 14.5527559),
        new("Szczecin", "Mazurska", "20", "70-444", 53.4340073, 14.5447254),
        new("Szczecin", "Krzywoustego", "10", "70-304", 53.4266384, 14.5419570),
        new("Szczecin", "Monte Cassino", "20", "71-431", 53.4404299, 14.5364192),
        new("Szczecin", "Ku Słońcu", "50", "71-046", 53.4231572, 14.4955320),
        new("Szczecin", "Rayskiego", "20", "70-442", 53.4317500, 14.5457412),
        new("Szczecin", "Mickiewicza", "50", "70-392", 53.4374659, 14.5281746),
        new("Szczecin", "Jana Pawła II", "30", "71-417", 53.4366623, 14.5433555),
        new("Szczecin", "Wyszyńskiego", "10", "70-201", 53.4244519, 14.5525751),
        new("Szczecin", "Niemierzyńska", "10", "71-436", 53.4465532, 14.5414994),
    ];

    /// <summary>Second shipping addresses (e.g. a workplace) of some clients, in the same city area.</summary>
    private static readonly Dictionary<int, DemoAddress> SecondAddresses = new()
    {
        [0] = new("Gdańsk", "Partyzantów", "20", "80-254", 54.3779210, 18.6030731),
        [1] = new("Gdańsk", "Kołobrzeska", "30", "80-394", 54.4029675, 18.5860851),
        [2] = new("Gdynia", "10 Lutego", "20", "81-361", 54.5197964, 18.5361289),
        [13] = new("Szczecin", "Śląska", "20", "70-433", 53.4352229, 14.5488735),
        [14] = new("Szczecin", "Wojska Polskiego", "100", "70-485", 53.4393587, 14.5333384),
    };

    private static readonly DemoAddress InactiveClientAddress = new("Gdynia", "Wielkopolska", "100", "81-534", 54.4811468, 18.5301627);
    /// <summary>Real building that "failed" geocoding – the admin has to pin it on the map.</summary>
    private static readonly DemoAddress NotGeocoded = new("Gdańsk", "Kraszewskiego", "10a", "80-447", null, null);

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
            var home = Addresses[i];
            shipping.Add(new DemoShippingAddress(client.Id, AddAddress(client, home, AddressType.Shipping, apartment, now).Id, home.Region));
            if (SecondAddresses.TryGetValue(i, out var second))
            {
                shipping.Add(new DemoShippingAddress(client.Id, AddAddress(client, second, AddressType.Shipping, apartment: null, now).Id, second.Region));
            }

            clients.Add((client, now.AddDays(-faker.Random.Number(20, 400)).AddMinutes(-faker.Random.Number(0, 600))));
        }

        // Edge cases (not used by demo orders).
        AddAddress(clients[5].Client, Addresses[5], AddressType.Billing, apartment: null, now);
        AddAddress(clients[7].Client, NotGeocoded, AddressType.Shipping, apartment: "4", now);
        var inactive = Client.Create("tomasz.nieaktywny", "tomasz.nieaktywny@wp.pl", "Tomasz", "Zieliński", "+48 511 000 111");
        AddAddress(inactive, InactiveClientAddress, AddressType.Shipping, apartment: "12", now);
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
        var address = client.AddAddress(new AddressDetails(type, demo.Street, demo.BuildingNumber, apartment, demo.City, demo.PostalCode, Country));
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

    private sealed record DemoAddress(string City, string Street, string BuildingNumber, string PostalCode, double? Latitude, double? Longitude)
    {
        public DemoRegion Region => City == "Szczecin" ? DemoRegion.Szczecin : DemoRegion.Tricity;
    }
}
