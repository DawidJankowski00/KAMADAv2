using Kamada.Shared.Contracts.Auth;
using MudBlazor;

namespace Kamada.MobileApp.Layout;

/// <summary>Each role has its own accent colour, so a worker with several roles always sees which one is active.</summary>
internal static class MobileTheme
{
    /// <summary>Courier blue: the login page and the courier role (unchanged from the courier app).</summary>
    public static MudTheme Default { get; } = Create(primary: "#1565c0", appBar: "#0d47a1");

    private static readonly MudTheme Admin = Create(primary: "#5e35b1", appBar: "#4527a0");

    public static MudTheme For(string? role) => role switch
    {
        Roles.Admin => Admin,
        _ => Default,
    };

    private static MudTheme Create(string primary, string appBar) => new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = primary,
            Secondary = "#6a1b9a",
            AppbarBackground = appBar,
            Background = "#f5f7fb",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
        },
    };
}
