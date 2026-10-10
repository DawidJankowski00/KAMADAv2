using Kamada.Modules.Catalog.Domain.Categories;
using Kamada.Modules.Catalog.Domain.Products;
using Kamada.Modules.Catalog.Infrastructure.Persistence;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Catalog.Infrastructure.Seeding;

/// <summary>
/// Demo catalog: a two-level category tree and 46 products of a local online shop. A few products show
/// edge cases – several categories, low stock, out of stock and one deactivated product.
/// </summary>
internal sealed class CatalogSeeder(CatalogDbContext db, DemoSeedData seedData) : IDataSeeder
{
    private static readonly (string Name, string[] Children)[] Tree =
    [
        ("Elektronika", ["Smartfony i akcesoria", "Komputery i peryferia", "Audio"]),
        ("Dom i kuchnia", ["Małe AGD", "Naczynia i garnki"]),
        ("Artykuły spożywcze", ["Kawa i herbata", "Słodycze i przekąski", "Napoje", "Pieczywo"]),
        ("Ogród", ["Narzędzia ogrodowe", "Nasiona i nawozy"]),
        ("Sport i rekreacja", ["Fitness", "Turystyka"]),
    ];

    private static readonly DemoProduct[] Products =
    [
        new("EL-SMA-001", "Ładowarka sieciowa USB-C 65 W", "Szybkie ładowanie telefonu i laptopa, GaN.", 129.99m, 80, ["Smartfony i akcesoria"]),
        new("EL-SMA-002", "Powerbank 20 000 mAh", "Dwa porty USB-C, wyświetlacz poziomu naładowania.", 159.00m, 45, ["Smartfony i akcesoria"]),
        new("EL-SMA-003", "Etui silikonowe na telefon", "Uniwersalne, matowe wykończenie.", 39.90m, 150, ["Smartfony i akcesoria"]),
        new("EL-SMA-004", "Kabel USB-C – USB-C 2 m", "Oplot nylonowy, 100 W.", 34.99m, 200, ["Smartfony i akcesoria"]),
        new("EL-SMA-005", "Uchwyt samochodowy magnetyczny", "Montaż na kratkę wentylacyjną.", 59.00m, 3, ["Smartfony i akcesoria"]),
        new("EL-KOM-001", "Mysz bezprzewodowa ergonomiczna", "Cicha praca, 6 przycisków, Bluetooth.", 119.00m, 60, ["Komputery i peryferia"]),
        new("EL-KOM-002", "Klawiatura mechaniczna TKL", "Przełączniki brązowe, podświetlenie.", 289.00m, 25, ["Komputery i peryferia"]),
        new("EL-KOM-003", "Monitor 27\" QHD IPS", "2560 × 1440, 75 Hz, regulacja wysokości.", 1149.00m, 12, ["Komputery i peryferia"]),
        new("EL-KOM-004", "Kamera internetowa Full HD", "Autofokus, dwa mikrofony.", 199.00m, 30, ["Komputery i peryferia"]),
        new("EL-KOM-005", "Dysk SSD przenośny 1 TB", "USB 3.2, do 1050 MB/s.", 379.00m, 0, ["Komputery i peryferia"]),
        new("EL-AUD-001", "Słuchawki bezprzewodowe ANC", "Aktywna redukcja szumów, 30 h pracy.", 449.00m, 35, ["Audio"]),
        new("EL-AUD-002", "Głośnik Bluetooth wodoodporny", "IP67, 12 h odtwarzania.", 229.00m, 40, ["Audio", "Turystyka"]),
        new("EL-AUD-003", "Radio internetowe z budzikiem", "Wi-Fi, DAB+, dwa alarmy.", 349.00m, 15, ["Audio"]),
        new("DK-AGD-001", "Czajnik elektryczny 1,7 l", "Stal nierdzewna, regulacja temperatury.", 149.00m, 50, ["Małe AGD"]),
        new("DK-AGD-002", "Ekspres przelewowy do kawy", "Dzbanek termiczny 1,2 l, programator.", 399.00m, 20, ["Małe AGD", "Kawa i herbata"]),
        new("DK-AGD-003", "Blender kielichowy 1200 W", "Kielich szklany 1,5 l, funkcja kruszenia lodu.", 269.00m, 18, ["Małe AGD"]),
        new("DK-AGD-004", "Toster na 2 kromki", "7 stopni opiekania, rozmrażanie.", 119.00m, 30, ["Małe AGD"]),
        new("DK-AGD-005", "Frytkownica beztłuszczowa 5 l", "8 programów, kosz z powłoką nieprzywierającą.", 459.00m, 14, ["Małe AGD"]),
        new("DK-NAC-001", "Zestaw garnków 8 elementów", "Stal nierdzewna, na indukcję.", 499.00m, 10, ["Naczynia i garnki"]),
        new("DK-NAC-002", "Patelnia granitowa 28 cm", "Powłoka nieprzywierająca, na indukcję.", 129.00m, 40, ["Naczynia i garnki"]),
        new("DK-NAC-003", "Deska do krojenia bambusowa", "Zestaw 3 desek w różnych rozmiarach.", 69.00m, 55, ["Naczynia i garnki"]),
        new("DK-NAC-004", "Komplet noży kuchennych w bloku", "5 noży ze stali nierdzewnej.", 249.00m, 16, ["Naczynia i garnki"]),
        new("SP-KAW-001", "Kawa ziarnista 1 kg – Brazylia", "Arabika 100 %, średnio palona.", 89.99m, 120, ["Kawa i herbata"]),
        new("SP-KAW-002", "Kawa mielona 500 g – espresso", "Mieszanka arabiki i robusty.", 42.99m, 140, ["Kawa i herbata"]),
        new("SP-KAW-003", "Herbata zielona sencha 100 g", "Liściasta, z Japonii.", 29.90m, 90, ["Kawa i herbata"]),
        new("SP-KAW-004", "Herbata czarna earl grey 50 torebek", "Z olejkiem bergamotowym.", 14.99m, 160, ["Kawa i herbata"]),
        new("SP-SLO-001", "Czekolada gorzka 70 % – 5 × 100 g", "Zestaw pięciu tabliczek.", 34.50m, 110, ["Słodycze i przekąski"]),
        new("SP-SLO-002", "Pierniki toruńskie w czekoladzie 300 g", "Tradycyjny przepis.", 18.99m, 130, ["Słodycze i przekąski"]),
        new("SP-SLO-003", "Mieszanka orzechów 500 g", "Nerkowce, migdały, orzechy laskowe.", 39.99m, 75, ["Słodycze i przekąski"]),
        new("SP-NAP-001", "Woda mineralna niegazowana 1,5 l", "Niskozmineralizowana.", 2.49m, 240, ["Napoje"]),
        new("SP-NAP-002", "Woda mineralna gazowana 1,5 l", "Wysokonasycona CO₂.", 2.49m, 180, ["Napoje"]),
        new("SP-NAP-003", "Sok jabłkowy 1 l", "100 % soku, tłoczony.", 6.99m, 90, ["Napoje"]),
        new("SP-NAP-004", "Sok pomarańczowy 1 l", "100 % soku, bez dodatku cukru.", 7.49m, 85, ["Napoje"]),
        new("SP-PIE-001", "Chleb razowy 500 g", "Na zakwasie, z mąki żytniej pełnoziarnistej.", 6.49m, 40, ["Pieczywo"]),
        new("SP-PIE-002", "Bułka grahamka", "Z mąki pszennej razowej.", 1.29m, 120, ["Pieczywo"]),
        new("OG-NAR-001", "Sekator nożycowy", "Ostrza ze stali hartowanej, do gałęzi do 20 mm.", 79.00m, 35, ["Narzędzia ogrodowe"]),
        new("OG-NAR-002", "Wąż ogrodowy 25 m z zestawem", "Pistolet zraszający i szybkozłączki.", 139.00m, 22, ["Narzędzia ogrodowe"]),
        new("OG-NAR-003", "Grabie do liści wachlarzowe", "Regulowana szerokość, trzonek aluminiowy.", 54.00m, 28, ["Narzędzia ogrodowe"]),
        new("OG-NAR-004", "Rękawice ogrodowe (3 pary)", "Powlekane lateksem.", 24.99m, 95, ["Narzędzia ogrodowe"]),
        new("OG-NAS-001", "Nasiona trawy uniwersalnej 5 kg", "Na trawnik rekreacyjny, ok. 200 m².", 119.00m, 30, ["Nasiona i nawozy"]),
        new("OG-NAS-002", "Nawóz do pomidorów 1 kg", "Granulowany, długo działający.", 27.99m, 60, ["Nasiona i nawozy"]),
        new("OG-NAS-003", "Zestaw nasion ziół – 6 odmian", "Bazylia, tymianek, oregano, mięta, koper, pietruszka.", 19.99m, 85, ["Nasiona i nawozy"]),
        new("SR-FIT-001", "Mata do ćwiczeń 10 mm", "Antypoślizgowa, z paskiem do noszenia.", 79.00m, 45, ["Fitness"]),
        new("SR-FIT-002", "Hantle regulowane 2 × 10 kg", "Zestaw z obciążeniem i gryfami.", 229.00m, 12, ["Fitness"]),
        new("SR-TUR-001", "Plecak turystyczny 30 l", "Z pokrowcem przeciwdeszczowym.", 189.00m, 25, ["Turystyka"]),
        new("SR-TUR-002", "Butelka termiczna 750 ml", "Utrzymuje temperaturę do 24 h.", 89.00m, 70, ["Turystyka", "Fitness"]),
    ];

    /// <summary>Withdrawn from the offer – shows deactivation in the admin panel.</summary>
    private const string DeactivatedSku = "EL-AUD-003";

    public int Order => SeedOrder.Catalog;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (await db.Products.AnyAsync(cancellationToken) || await db.Categories.AnyAsync(cancellationToken))
        {
            return;
        }

        var categories = new Dictionary<string, Guid>();
        foreach (var (name, children) in Tree)
        {
            var root = Category.Create(name, parentId: null);
            db.Categories.Add(root);
            foreach (var child in children)
            {
                var category = Category.Create(child, root.Id);
                db.Categories.Add(category);
                categories.Add(child, category.Id);
            }
        }

        var products = new List<Product>();
        foreach (var demo in Products)
        {
            var product = Product.Create(demo.Name, demo.Description, demo.Sku, demo.Price, demo.Stock, demo.Categories.Select(c => categories[c])).Value;
            if (demo.Sku == DeactivatedSku)
            {
                product.Deactivate();
            }

            products.Add(product);
        }

        db.Products.AddRange(products);
        await db.SaveChangesAsync(cancellationToken);
        // Low-stock products stay out of demo orders, so every order can be placed.
        seedData.ProductIds = [.. products.Where(p => p.IsActive && p.StockQuantity >= 10).Select(p => p.Id)];
    }

    private sealed record DemoProduct(string Sku, string Name, string Description, decimal Price, int Stock, string[] Categories);
}
