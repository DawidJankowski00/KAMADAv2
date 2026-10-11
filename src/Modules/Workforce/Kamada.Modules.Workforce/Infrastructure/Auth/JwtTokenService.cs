using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Infrastructure.Auth;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Kamada.Modules.Workforce.Infrastructure.Auth;

internal sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider clock) : ITokenService
{
    private readonly JwtOptions _options = options.Value;
    private readonly JsonWebTokenHandler _handler = new();

    public TimeSpan RefreshTokenLifetime => _options.RefreshTokenLifetime;

    public AccessToken CreateAccessToken(Worker worker)
    {
        var now = clock.GetUtcNow();
        var expiresAt = now + _options.AccessTokenLifetime;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            Subject = new ClaimsIdentity(
            [
                new Claim(KamadaClaimTypes.Subject, worker.Id.ToString()),
                new Claim(KamadaClaimTypes.Name, worker.Username),
                new Claim(KamadaClaimTypes.GivenName, worker.Name),
                new Claim(KamadaClaimTypes.FamilyName, worker.Surname),
                new Claim(KamadaClaimTypes.Role, worker.Roles[0].ToRoleName()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
                SecurityAlgorithms.HmacSha256),
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }

    public GeneratedRefreshToken GenerateRefreshToken()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        return new GeneratedRefreshToken(token, HashRefreshToken(token));
    }

    /// <summary>
    /// SHA-256 is sufficient here (unlike for passwords): the token has 512 bits of entropy, so it cannot be brute-forced.
    /// </summary>
    public string HashRefreshToken(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
