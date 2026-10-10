using Kamada.Modules.Customers.Application.Clients;
using Kamada.Shared.Kernel.Text;

namespace Kamada.Modules.Customers.Application.Lookup;

/// <summary>Generates usernames for clients created from an order draft: <c>jan.kowalski</c>, <c>jan.kowalski2</c>, …</summary>
internal static class UsernameSuggestion
{
    public const string Fallback = "klient";

    private const int MaxSuffixLength = 4;

    /// <summary>"Łucja", "Nowak-Wiśniewska" → "lucja.nowak-wisniewska"; <see cref="Fallback"/> when nothing usable was given.</summary>
    public static string Base(string? name, string? surname)
    {
        var parts = new[] { name, surname }
            .Select(Ascii)
            .Where(p => p.Length > 0)
            .ToList();
        var candidate = string.Join('.', parts);
        if (candidate.Length < 3)
        {
            return Fallback;
        }

        return candidate.Length > CustomerRules.UsernameMaxLength - MaxSuffixLength
            ? candidate[..(CustomerRules.UsernameMaxLength - MaxSuffixLength)].TrimEnd('.', '-')
            : candidate;
    }

    /// <summary>The base itself when free, otherwise the base with the lowest free numeric suffix (2, 3, …).</summary>
    public static string FirstFree(string baseName, IReadOnlySet<string> taken)
    {
        if (!taken.Contains(baseName))
        {
            return baseName;
        }

        var suffix = 2;
        while (taken.Contains($"{baseName}{suffix}"))
        {
            suffix++;
        }

        return $"{baseName}{suffix}";
    }

    private static string Ascii(string? value) =>
        string.Join('-', TextSimilarity.Tokens(value).Select(t => string.Concat(t.Where(char.IsAsciiLetterOrDigit))).Where(t => t.Length > 0));
}
