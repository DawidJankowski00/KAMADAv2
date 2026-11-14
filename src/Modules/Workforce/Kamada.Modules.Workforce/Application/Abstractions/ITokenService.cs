using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.Application.Abstractions;

internal sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

internal sealed record GeneratedToken(string Token, string Hash);

internal interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    AccessToken CreateAccessToken(Worker worker);

    /// <summary>
    /// Cryptographically random opaque token (refresh tokens, password reset tokens) plus the SHA-256 hash that is
    /// persisted; the token itself is never stored.
    /// </summary>
    GeneratedToken GenerateOpaqueToken();

    string HashOpaqueToken(string token);
}
