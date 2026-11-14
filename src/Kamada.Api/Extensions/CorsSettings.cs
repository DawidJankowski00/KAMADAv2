namespace Kamada.Api.Extensions;

/// <summary>
/// Section <c>Cors</c>: the browser origins of the two web apps. Only these may call the API from a browser; the
/// apps send a bearer token, so no cookies or credentials are allowed cross-origin.
/// </summary>
internal sealed class CorsSettings
{
    public const string SectionName = "Cors";

    /// <summary>Exact origins (<c>https://panel.example.com</c>); a trailing slash is ignored, a path or <c>*</c> is not allowed.</summary>
    public string[] AllowedOrigins { get; init; } = [];

    /// <summary>The configured origins in canonical form, or the first invalid entry.</summary>
    public (string[] Origins, string? Invalid) Normalize()
    {
        var origins = new List<string>();
        foreach (var entry in AllowedOrigins.Where(o => !string.IsNullOrWhiteSpace(o)))
        {
            var trimmed = entry.Trim().TrimEnd('/');
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var uri)
                || uri.Scheme is not ("http" or "https")
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || trimmed.Contains('*', StringComparison.Ordinal))
            {
                return ([], entry);
            }

            origins.Add(uri.GetLeftPart(UriPartial.Authority));
        }

        return ([.. origins.Distinct(StringComparer.OrdinalIgnoreCase)], null);
    }
}
