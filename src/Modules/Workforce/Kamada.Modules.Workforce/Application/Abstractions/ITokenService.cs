using Kamada.Modules.Workforce.Domain.Workers;

namespace Kamada.Modules.Workforce.Application.Abstractions;

internal sealed record AccessToken(string Token, DateTimeOffset ExpiresAt);

internal sealed record GeneratedRefreshToken(string Token, string Hash);

internal interface ITokenService
{
    TimeSpan RefreshTokenLifetime { get; }

    AccessToken CreateAccessToken(Worker worker);

    /// <summary>Cryptographically random opaque token plus the hash that is persisted.</summary>
    GeneratedRefreshToken GenerateRefreshToken();

    string HashRefreshToken(string token);
}
