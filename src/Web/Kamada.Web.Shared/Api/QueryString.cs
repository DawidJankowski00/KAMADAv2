using System.Globalization;

namespace Kamada.Web.Shared.Api;

/// <summary>Builds <c>?a=1&amp;b=x</c> from name/value pairs, skipping nulls and empty strings.</summary>
public static class QueryString
{
    public static string Build(params (string Name, object? Value)[] parameters)
    {
        var parts = parameters
            .Select(p => (p.Name, Value: Format(p.Value)))
            .Where(p => !string.IsNullOrEmpty(p.Value))
            .Select(p => $"{Uri.EscapeDataString(p.Name)}={Uri.EscapeDataString(p.Value!)}")
            .ToList();

        return parts.Count == 0 ? string.Empty : "?" + string.Join('&', parts);
    }

    private static string? Format(object? value) => value switch
    {
        null => null,
        string s => s.Trim(),
        bool b => b ? "true" : "false",
        DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
        DateTime d => new DateTimeOffset(d).ToString("O", CultureInfo.InvariantCulture),
        DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };
}
