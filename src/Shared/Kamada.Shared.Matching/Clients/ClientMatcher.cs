using Kamada.Shared.Contracts.Assistant;
using Kamada.Shared.Matching.Text;

namespace Kamada.Shared.Matching.Clients;

/// <summary>
/// Ranks clients against a spoken/typed mention. Exact phone number (compared as digits, country prefix ignored)
/// and exact e-mail are certain; names are fuzzy (diacritics folded, Jaro-Winkler, either word order).
/// </summary>
public static class ClientMatcher
{
    /// <summary>Name similarity below this is not worth showing as a candidate.</summary>
    public const double MinNameScore = 0.8;

    private const int NationalNumberLength = 9;

    public static IReadOnlyList<ClientCandidate> Rank(ClientMention mention, IEnumerable<ClientRow> clients, int limit)
    {
        var phone = PhoneDigits(mention.PhoneNumber);
        var email = NormalizeEmail(mention.Email);

        return clients
            .Select(client => Score(client, mention, phone, email))
            .OfType<ClientCandidate>()
            .OrderByDescending(c => c.Score)
            .ThenBy(c => c.Reason)
            .ThenBy(c => c.Surname, StringComparer.Ordinal)
            .Take(limit)
            .ToList();
    }

    /// <summary>"+48 600-100-200" → "600100200": digits only, the last 9 for longer (international) numbers.</summary>
    public static string PhoneDigits(string? phoneNumber)
    {
        var digits = string.Concat((phoneNumber ?? string.Empty).Where(char.IsAsciiDigit));
        return digits.Length > NationalNumberLength ? digits[^NationalNumberLength..] : digits;
    }

    /// <summary>Similarity of the mentioned name and surname to a client's, 0 … 1. Only one part mentioned counts less.</summary>
    public static double NameScore(ClientMention mention, ClientRow client)
    {
        var hasName = !string.IsNullOrWhiteSpace(mention.Name);
        var hasSurname = !string.IsNullOrWhiteSpace(mention.Surname);

        if (hasName && hasSurname)
        {
            var direct = (TextSimilarity.Similarity(mention.Name, client.Name) + TextSimilarity.Similarity(mention.Surname, client.Surname)) / 2;
            var swapped = (TextSimilarity.Similarity(mention.Name, client.Surname) + TextSimilarity.Similarity(mention.Surname, client.Name)) / 2;
            return Math.Max(direct, swapped);
        }

        if (hasSurname)
        {
            return 0.9 * TextSimilarity.Similarity(mention.Surname, client.Surname);
        }

        return hasName ? 0.8 * TextSimilarity.Similarity(mention.Name, client.Name) : 0;
    }

    private static ClientCandidate? Score(ClientRow client, ClientMention mention, string phone, string? email)
    {
        if (phone.Length == NationalNumberLength && PhoneDigits(client.PhoneNumber) == phone)
        {
            return Candidate(client, 1, ClientMatchReason.Phone);
        }

        if (email is not null && string.Equals(NormalizeEmail(client.Email), email, StringComparison.Ordinal))
        {
            return Candidate(client, 1, ClientMatchReason.Email);
        }

        var nameScore = NameScore(mention, client);
        return nameScore >= MinNameScore ? Candidate(client, Math.Round(nameScore, 3), ClientMatchReason.Name) : null;
    }

    private static string? NormalizeEmail(string? email) =>
        string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();

    private static ClientCandidate Candidate(ClientRow client, double score, ClientMatchReason reason) =>
        new(client.Id, client.Name, client.Surname, client.Email, client.PhoneNumber, score, reason);
}
