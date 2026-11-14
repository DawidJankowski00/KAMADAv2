namespace Kamada.Shared.Infrastructure.Auth;

public static class AuthorizationPolicies
{
    /// <summary>Any signed-in worker (own account, sessions, 2FA).</summary>
    public const string SignedIn = nameof(SignedIn);

    public const string AdminOnly = nameof(AdminOnly);
    public const string CourierOnly = nameof(CourierOnly);

    /// <summary>Taking orders: admins and sellers.</summary>
    public const string SalesAccess = nameof(SalesAccess);
}
