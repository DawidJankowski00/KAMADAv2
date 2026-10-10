using Kamada.Modules.Customers.Contracts;
using Kamada.Shared.Kernel.Text;

namespace Kamada.Modules.Customers.Application.Lookup;

/// <summary>Searchable projection of an active shipping address.</summary>
internal sealed record AddressRow(
    Guid Id,
    string Street,
    string BuildingNumber,
    string? ApartmentNumber,
    string City,
    string PostalCode,
    double? Latitude,
    double? Longitude);

/// <summary>
/// Decides whether a mentioned address is one the client already has: same street (fuzzy, without "ul."/"al." prefixes),
/// same building and apartment (spaces and case ignored); the city only chooses between several matches.
/// </summary>
internal static class AddressMatcher
{
    private const double MinStreetSimilarity = 0.9;
    private const double MinCitySimilarity = 0.85;

    private static readonly HashSet<string> StreetPrefixes = new(
        ["ul", "ulica", "al", "aleja", "aleje", "os", "osiedle", "pl", "plac", "street", "st"],
        StringComparer.Ordinal);

    public static AddressRow? FindSame(AddressMention mention, IReadOnlyCollection<AddressRow> addresses)
    {
        if (string.IsNullOrWhiteSpace(mention.Street) || string.IsNullOrWhiteSpace(mention.BuildingNumber))
        {
            return null;
        }

        var street = StreetName(mention.Street);
        var building = TextSimilarity.Compact(mention.BuildingNumber);
        var sameBuilding = addresses
            .Where(a => TextSimilarity.JaroWinkler(street, StreetName(a.Street)) >= MinStreetSimilarity)
            .Where(a => TextSimilarity.Compact(a.BuildingNumber) == building)
            .ToList();

        // The city only breaks ties: a client rarely has the same street and number in two cities, while a misheard
        // or misread city is common.
        var inSameCity = sameBuilding
            .Where(a => !string.IsNullOrWhiteSpace(mention.City) && TextSimilarity.Similarity(mention.City, a.City) >= MinCitySimilarity)
            .ToList();
        if (inSameCity.Count > 0)
        {
            sameBuilding = inSameCity;
        }

        var apartment = TextSimilarity.Compact(mention.ApartmentNumber);
        var exact = sameBuilding.Where(a => TextSimilarity.Compact(a.ApartmentNumber) == apartment).ToList();
        if (exact.Count == 1)
        {
            return exact[0];
        }

        // No apartment said, but the client has exactly one address in that building: that is the one.
        return apartment.Length == 0 && exact.Count == 0 && sameBuilding.Count == 1 ? sameBuilding[0] : null;
    }

    /// <summary>"ul. Wały Chrobrego" → "waly chrobrego".</summary>
    public static string StreetName(string? street)
    {
        var tokens = TextSimilarity.Tokens(street);
        var start = 0;
        while (start < tokens.Length - 1 && StreetPrefixes.Contains(tokens[start]))
        {
            start++;
        }

        return string.Join(' ', tokens[start..]);
    }
}
