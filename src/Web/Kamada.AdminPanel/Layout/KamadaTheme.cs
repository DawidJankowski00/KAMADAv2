using MudBlazor;

namespace Kamada.AdminPanel.Layout;

internal static class KamadaTheme
{
    public static MudTheme Theme { get; } = new()
    {
        PaletteLight = new PaletteLight
        {
            Primary = "#1565c0",
            Secondary = "#6a1b9a",
            AppbarBackground = "#0d47a1",
            Background = "#f5f7fb",
            DrawerBackground = "#ffffff",
        },
        LayoutProperties = new LayoutProperties
        {
            DefaultBorderRadius = "8px",
        },
    };
}
