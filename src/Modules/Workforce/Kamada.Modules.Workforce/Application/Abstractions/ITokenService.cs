using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.Application.Abstractions;

internal sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

internal sealed record GeneratedToken(string Token, string Hash);

internal interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    /// <param name="sessionId">The refresh token family (one per sign-in); sent as the <c>sid</c> claim.</param>
    /// <param name="twoFactorSetupRequired">Adds the <c>2fa_setup</c> claim: the API then answers only account endpoints.</param>
    AccessToken CreateAccessToken(Worker worker, Guid sessionId, bool twoFactorSetupRequired = false);

    /// <summary>
    /// Cryptographically random opaque token (refresh tokens, password reset tokens) plus the SHA-256 hash that is
    /// persisted; the token itself is never stored.
    /// </summary>
    GeneratedToken GenerateOpaqueToken();

    string HashOpaqueToken(string token);
}
