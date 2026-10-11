using MudBlazor;

namespace Kamada.MobileApp.Layout;

internal static class MobileTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565c0",
            Secondary = "#6a1b9a",
            AppbarBackground = "#0d47a1",
            Background = "#f5f7fb",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "10px",
        },
    };
}
