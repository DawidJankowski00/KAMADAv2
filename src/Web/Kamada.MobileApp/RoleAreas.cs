using System.Reflection;
using Kamada.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using MudBlazor;

namespace Kamada.MobileApp;

/// <summary>
/// Every role has its own area of the app. A page belongs to the role named in its <c>[Authorize(Roles = ...)]</c>.
/// </summary>
internal static class RoleAreas
{
    /// <summary>Base-relative URL of the role's start page; null for a role without views in this app.</summary>
    public static string? HomeOf(string role) => role switch
    {
        Roles.Courier => "courier",
        Roles.Admin => "dashboard",
        Roles.Seller => "seller",
        _ => null,
    };

    public static string IconOf(string role) => role switch
    {
        Roles.Courier => Icons.Material.Filled.LocalShipping,
        Roles.Admin => Icons.Material.Filled.AdminPanelSettings,
        Roles.Seller => Icons.Material.Filled.PointOfSale,
        _ => Icons.Material.Filled.Badge,
    };

    /// <summary>The role whose area the page belongs to; null for pages shared by every role.</summary>
    public static string? RoleOf(Type pageType) => pageType
        .GetCustomAttributes<AuthorizeAttribute>(inherit: true)
        .Select(attribute => attribute.Roles)
        .FirstOrDefault(roles => !string.IsNullOrEmpty(roles));
}
