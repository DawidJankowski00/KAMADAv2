using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>Section <c>Auth:PasswordPolicy</c>.</summary>
internal sealed class PasswordPolicyOptions
{
    public const string SectionName = "Auth:PasswordPolicy";

    /// <summary>
    /// Optional text file with more passwords to refuse (one per line, <c>#</c> starts a comment), e.g. a list of
    /// breached passwords. Added to the built-in list.
    /// </summary>
    public string? BlocklistFile { get; init; }
}

/// <summary>
/// Rules for a new password (OWASP ASVS 4 level 2, NIST SP 800-63B): long enough, not a well-known password, no
/// composition rules (they push people towards <c>Password1!</c>). Checked when a password is set, not at sign-in.
/// </summary>
internal sealed class PasswordPolicy
{
    public const int MinLength = 12;
    public const int MaxLength = 128;

    private readonly HashSet<string> _blocked;

    public PasswordPolicy(IOptions<PasswordPolicyOptions> options)
    {
        _blocked = new HashSet<string>(ReadBuiltInList(), StringComparer.OrdinalIgnoreCase);
        if (options.Value.BlocklistFile is { Length: > 0 } file)
        {
            _blocked.UnionWith(ReadLines(File.ReadLines(file)));
        }
    }

    /// <summary>A password from the list, or one character repeated (<c>aaaaaaaaaaaa</c>).</summary>
    public bool IsCommon(string password)
    {
        var candidate = password.Trim();
        return _blocked.Contains(candidate) || (candidate.Length > 0 && candidate.All(c => c == candidate[0]));
    }

    private static IEnumerable<string> ReadBuiltInList()
    {
        using var stream = typeof(PasswordPolicy).Assembly.GetManifestResourceStream(typeof(PasswordPolicy).Namespace + ".common-passwords.txt")
            ?? throw new InvalidOperationException("The built-in password list is missing from the assembly.");
        using var reader = new StreamReader(stream);
        return [.. ReadLines(ReadAll(reader))];
    }

    private static IEnumerable<string> ReadAll(StreamReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }

    private static IEnumerable<string> ReadLines(IEnumerable<string> lines) =>
        lines.Select(line => line.Trim()).Where(line => line.Length > 0 && !line.StartsWith('#'));
}
