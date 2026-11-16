namespace Kamada.Web.Shared.Account;

/// <summary>
/// A short Polish description of a session's device from its User-Agent ("Chrome · Android"). Only a hint for the
/// worker: User-Agents can be anything, so unknown parts are left out instead of guessed.
/// </summary>
public static class DeviceLabel
{
    // Order matters: Edge and Opera also say "Chrome", Chrome also says "Safari", Android also says "Linux".
    private static readonly (string Token, string Name)[] Browsers =
    [
        ("Edg/", "Edge"),
        ("OPR/", "Opera"),
        ("SamsungBrowser/", "Samsung Internet"),
        ("Firefox/", "Firefox"),
        ("Chrome/", "Chrome"),
        ("Safari/", "Safari"),
    ];

    private static readonly (string Token, string Name)[] Systems =
    [
        ("Android", "Android"),
        ("iPhone", "iPhone"),
        ("iPad", "iPad"),
        ("Windows", "Windows"),
        ("Mac OS X", "macOS"),
        ("CrOS", "ChromeOS"),
        ("Linux", "Linux"),
    ];

    public static string Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent))
        {
            return "Nieznane urządzenie";
        }

        var browser = Browsers.FirstOrDefault(b => userAgent.Contains(b.Token, StringComparison.Ordinal)).Name;
        var system = Systems.FirstOrDefault(s => userAgent.Contains(s.Token, StringComparison.Ordinal)).Name;
        return (browser, system) switch
        {
            (null, null) => "Nieznana przeglądarka",
            (not null, null) => browser,
            (null, not null) => system,
            _ => $"{browser} · {system}",
        };
    }

    public static bool IsPhone(string? userAgent) =>
        userAgent is not null && (userAgent.Contains("Mobile", StringComparison.Ordinal) || userAgent.Contains("Android", StringComparison.Ordinal));
}
