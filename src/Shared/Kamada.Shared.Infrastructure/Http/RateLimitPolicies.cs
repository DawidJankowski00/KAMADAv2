namespace Kamada.Shared.Infrastructure.Http;

public static class RateLimitPolicies
{
    /// <summary>Per-client-IP limit on authentication endpoints (brute-force protection).</summary>
    public const string Login = "login";

    /// <summary>Per-user limit on local AI inference (order assistant), which is expensive on CPU.</summary>
    public const string Assistant = "assistant";
}
