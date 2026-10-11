namespace Kamada.Shared.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    public const string AdminOnly = nameof(AdminOnly);
    public const string CourierOnly = nameof(CourierOnly);

    /// <summary>Taking orders: admins and sellers.</summary>
    public const string SalesAccess = nameof(SalesAccess);
}
