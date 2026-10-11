using System.Globalization;
using System.Text;
using Kamada.Shared.Contracts.Catalog;
using Kamada.Shared.Contracts.Customers;

namespace Kamada.Web.Shared.Offline;

/// <summary>
/// Search over the device's copy of clients and products, close to what the API's search finds: every word of the text
/// must appear in one of the fields, ignoring case and Polish diacritics (a seller typing on a phone often skips them).
/// </summary>
public static class LocalSearch
{
    private static readonly CultureInfo Polish = CultureInfo.GetCultureInfo("pl-PL");

    public static IEnumerable<ClientLookupDto> Clients(IEnumerable<ClientLookupDto> clients, string? text) =>
        Matching(clients, text, c => [c.Name, c.Surname, c.Email, c.Username, c.PhoneNumber, Digits(c.PhoneNumber)])
            .OrderBy(c => c.Surname, StringComparer.Create(Polish, ignoreCase: true))
            .ThenBy(c => c.Name, StringComparer.Create(Polish, ignoreCase: true));

    public static IEnumerable<ProductLookupDto> Products(IEnumerable<ProductLookupDto> products, string? text) =>
        Matching(products, text, p => [p.Name, p.Sku, .. p.Categories.Select(c => c.Name)])
            .OrderBy(p => p.Name, StringComparer.Create(Polish, ignoreCase: true));

    /// <summary>Lower case without diacritics; <c>ł</c> has no decomposition, so it is mapped by hand.</summary>
    public static string Fold(string value)
    {
        var decomposed = value.ToLower(Polish).Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var folded = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                folded.Append(c);
            }
        }

        return folded.ToString();
    }

    private static IEnumerable<T> Matching<T>(IEnumerable<T> items, string? text, Func<T, IEnumerable<string>> fields)
    {
        var words = Fold(text ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return words.Length == 0
            ? items
            : items.Where(item =>
            {
                var haystack = fields(item).Select(Fold).ToList();
                return words.All(word => haystack.Any(field => field.Contains(word, StringComparison.Ordinal)));
            });
    }

    private static string Digits(string value) => new([.. value.Where(char.IsAsciiDigit)]);
}
