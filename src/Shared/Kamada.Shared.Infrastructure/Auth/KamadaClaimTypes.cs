namespace Kamada.Shared.Infrastructure.Auth;

/// <summary>
/// Short JWT claim names (inbound claim mapping is disabled, so these are what handlers see).
/// </summary>
public static class KamadaClaimTypes
{
    public const string Subject = "sub";
    public const string Name = "name";
    public const string Role = "role";
    public const string GivenName = "given_name";
    public const string FamilyName = "family_name";
}
