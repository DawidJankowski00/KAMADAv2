using System.Globalization;
using System.Text;

namespace Kamada.Shared.Kernel.Text;

/// <summary>
/// Pure, deterministic fuzzy text matching used to look up spoken or typed names (clients, streets, products).
/// Text is folded first (lower case, diacritics removed – also <c>ł → l</c>, punctuation → spaces),
/// then compared with Jaro-Winkler, which rewards a common prefix and therefore tolerates Polish inflected
/// endings ("wód niegazowanych" ≈ "woda niegazowana").
/// </summary>
public static class TextSimilarity
{
    private const double WinklerPrefixScale = 0.1;
    private const int WinklerMaxPrefix = 4;

    /// <summary>"Łódź, ul. Piotrkowska 1/2" → "lodz ul piotrkowska 1 2".</summary>
    public static string Fold(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var decomposed = text.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        var pendingSpace = false;
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var folded = FoldLetter(char.ToLowerInvariant(c));
            if (char.IsLetterOrDigit(folded))
            {
                if (pendingSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(folded);
                pendingSpace = false;
            }
            else
            {
                pendingSpace = true;
            }
        }

        return builder.ToString();
    }

    /// <summary>Folded words of <paramref name="text"/>.</summary>
    public static string[] Tokens(string? text) =>
        Fold(text).Split(' ', StringSplitOptions.RemoveEmptyEntries);

    /// <summary>Folded text without spaces – "15 A" and "15a" compare equal.</summary>
    public static string Compact(string? text) => Fold(text).Replace(" ", string.Empty, StringComparison.Ordinal);

    /// <summary>Jaro-Winkler similarity of the folded texts, 0 (different) … 1 (equal).</summary>
    public static double Similarity(string? first, string? second) => JaroWinkler(Fold(first), Fold(second));

    /// <summary>
    /// How well <paramref name="query"/> is covered by <paramref name="candidate"/>, 0 … 1: every query word is paired
    /// with its most similar candidate word (numbers must be equal). Candidate words left unmatched lower the score
    /// slightly, so "chleb razowy" prefers "Chleb razowy" over "Chleb razowy z ziarnami".
    /// </summary>
    public static double PhraseScore(string? query, string? candidate)
    {
        var queryTokens = Tokens(query);
        var candidateTokens = Tokens(candidate);
        if (queryTokens.Length == 0 || candidateTokens.Length == 0)
        {
            return 0;
        }

        var used = new bool[candidateTokens.Length];
        var total = 0.0;
        foreach (var token in queryTokens)
        {
            var (bestIndex, bestScore) = (-1, 0.0);
            for (var i = 0; i < candidateTokens.Length; i++)
            {
                var score = TokenScore(token, candidateTokens[i]);
                if (score > bestScore || (score == bestScore && bestIndex >= 0 && used[bestIndex] && !used[i]))
                {
                    (bestIndex, bestScore) = (i, score);
                }
            }

            if (bestIndex >= 0)
            {
                used[bestIndex] = true;
            }

            total += bestScore;
        }

        var coverage = total / queryTokens.Length;
        var unmatched = used.Count(u => !u) / (double)candidateTokens.Length;
        return coverage * (1 - (0.15 * unmatched));
    }

    /// <summary>Jaro-Winkler similarity of two (already folded) strings.</summary>
    public static double JaroWinkler(string first, string second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (first.Length == 0 && second.Length == 0)
        {
            return 1;
        }

        if (first.Length == 0 || second.Length == 0)
        {
            return 0;
        }

        if (string.Equals(first, second, StringComparison.Ordinal))
        {
            return 1;
        }

        var jaro = Jaro(first, second);
        var prefix = 0;
        while (prefix < Math.Min(WinklerMaxPrefix, Math.Min(first.Length, second.Length)) && first[prefix] == second[prefix])
        {
            prefix++;
        }

        return jaro + (prefix * WinklerPrefixScale * (1 - jaro));
    }

    private static double TokenScore(string query, string candidate)
    {
        var queryIsNumber = query.All(char.IsAsciiDigit);
        var candidateIsNumber = candidate.All(char.IsAsciiDigit);
        if (queryIsNumber || candidateIsNumber)
        {
            return string.Equals(query, candidate, StringComparison.Ordinal) ? 1 : 0;
        }

        return JaroWinkler(query, candidate);
    }

    private static double Jaro(string first, string second)
    {
        var window = Math.Max(0, (Math.Max(first.Length, second.Length) / 2) - 1);
        var firstMatched = new bool[first.Length];
        var secondMatched = new bool[second.Length];

        var matches = 0;
        for (var i = 0; i < first.Length; i++)
        {
            var from = Math.Max(0, i - window);
            var to = Math.Min(second.Length - 1, i + window);
            for (var j = from; j <= to; j++)
            {
                if (!secondMatched[j] && first[i] == second[j])
                {
                    firstMatched[i] = true;
                    secondMatched[j] = true;
                    matches++;
                    break;
                }
            }
        }

        if (matches == 0)
        {
            return 0;
        }

        var transpositions = 0;
        var k = 0;
        for (var i = 0; i < first.Length; i++)
        {
            if (!firstMatched[i])
            {
                continue;
            }

            while (!secondMatched[k])
            {
                k++;
            }

            if (first[i] != second[k])
            {
                transpositions++;
            }

            k++;
        }

        double m = matches;
        return ((m / first.Length) + (m / second.Length) + ((m - (transpositions / 2.0)) / m)) / 3;
    }

    /// <summary>Letters that Unicode decomposition does not split into base letter + mark.</summary>
    private static char FoldLetter(char c) => c switch
    {
        'ł' => 'l',
        'ø' => 'o',
        'đ' => 'd',
        'ß' => 's',
        _ => c,
    };
}
