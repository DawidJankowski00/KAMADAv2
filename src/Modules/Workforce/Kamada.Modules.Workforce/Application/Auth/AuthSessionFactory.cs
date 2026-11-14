using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>
/// Issues access + refresh token pairs. The caller is responsible for saving changes.
/// </summary>
internal sealed class AuthSessionFactory(IWorkforceDbContext db, ITokenService tokens, TimeProvider clock)
{
    public TokenResponse Start(Worker worker, SessionClient client)
    {
        var generated = tokens.GenerateOpaqueToken();
        var refreshToken = RefreshToken.Issue(worker.Id, generated.Hash, clock.GetUtcNow(), tokens.RefreshTokenLifetime, client);
        db.RefreshTokens.Add(refreshToken);
        return CreateResponse(worker, generated.Token, refreshToken);
    }

    public TokenResponse Rotate(Worker worker, RefreshToken current, SessionClient client)
    {
        var generated = tokens.GenerateOpaqueToken();
        var successor = current.Rotate(generated.Hash, clock.GetUtcNow(), tokens.RefreshTokenLifetime, client);
        db.RefreshTokens.Add(successor);
        return CreateResponse(worker, generated.Token, successor);
    }

    private TokenResponse CreateResponse(Worker worker, string rawRefreshToken, RefreshToken refreshToken)
    {
        var accessToken = tokens.CreateAccessToken(worker, refreshToken.FamilyId);
        return new TokenResponse(
            accessToken.Token,
            accessToken.ExpiresAt,
            rawRefreshToken,
            refreshToken.ExpiresAt,
            worker.ToAuthenticatedUser());
    }
}
