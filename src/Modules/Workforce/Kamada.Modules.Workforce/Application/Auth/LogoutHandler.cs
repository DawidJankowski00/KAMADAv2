using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Shared.Contracts.Auth;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Auth;

internal sealed class LogoutHandler(IWorkforceDbContext db, ITokenService tokens, TimeProvider clock)
{
    /// <summary>
    /// Revokes the presented refresh token if it belongs to the caller. Idempotent: unknown tokens are ignored
    /// so the endpoint cannot be used to probe for valid tokens.
    /// </summary>
    public async Task HandleAsync(Guid workerId, LogoutRequest request, CancellationToken cancellationToken)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var token = await db.RefreshTokens.SingleOrDefaultAsync(
            t => t.TokenHash == hash && t.WorkerId == workerId,
            cancellationToken);

        if (token is not null)
        {
            token.Revoke(clock.GetUtcNow());
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
