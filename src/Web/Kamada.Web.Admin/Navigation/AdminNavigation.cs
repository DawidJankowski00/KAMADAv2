using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;

namespace Kamada.Web.Admin.Navigation;

/// <summary>A link in the admin menu. <paramref name="Href"/> is base-relative.</summary>
public sealed record AdminNavLink(string Title, string Href, string Icon, NavLinkMatch Match = NavLinkMatch.Prefix);

/// <summary>A titled group of links in the admin menu.</summary>
public sealed record AdminNavSection(string Title, string Icon, IReadOnlyList<AdminNavLink> Links);

/// <summary>
/// The admin menu, used by the web panel's drawer and the phone app's drawer, so both always offer the same pages.
/// </summary>
public static class AdminNavigation
{
    public static AdminNavLink Dashboard { get; } =
        new("Pulpit", "dashboard", Icons.Material.Filled.Dashboard, NavLinkMatch.All);

    public static IReadOnlyList<AdminNavSection> Sections { get; } =
    [
        new("Sprzedaż", Icons.Material.Filled.ShoppingCart,
        [
            new("Zamówienia", "orders", Icons.Material.Filled.ReceiptLong),
            new("Nowe zamówienie", "orders/new", Icons.Material.Filled.AddShoppingCart, NavLinkMatch.All),
            new("Klienci", "clients", Icons.Material.Filled.People),
        ]),
        new("Dostawy", Icons.Material.Filled.LocalShipping,
        [
            new("Trasy", "delivery-runs", Icons.Material.Filled.Route),
            new("Nowa trasa", "delivery-runs/new", Icons.Material.Filled.AddRoad, NavLinkMatch.All),
            new("Lokalizacje", "route-locations", Icons.Material.Filled.Warehouse),
        ]),
        new("Katalog", Icons.Material.Filled.Inventory2,
        [
            new("Produkty", "products", Icons.Material.Filled.Inventory),
            new("Kategorie", "categories", Icons.Material.Filled.AccountTree),
        ]),
        new("Administracja", Icons.Material.Filled.AdminPanelSettings,
        [
            new("Pracownicy", "workers", Icons.Material.Filled.Badge),
        ]),
    ];
}
