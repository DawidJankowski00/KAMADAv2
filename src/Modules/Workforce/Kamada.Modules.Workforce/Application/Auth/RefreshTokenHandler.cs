using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Workforce.Application.Auth;

internal sealed partial class RefreshTokenHandler(
    IWorkforceDbContext db,
    ITokenService tokens,
    AuthSessionFactory sessions,
    TimeProvider clock,
    ILogger<RefreshTokenHandler> logger)
{
    public async Task<Result<TokenResponse>> HandleAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var hash = tokens.HashRefreshToken(request.RefreshToken);
        var current = await db.RefreshTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (current is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var now = clock.GetUtcNow();
        if (current.IsRevoked)
        {
            // A rotated token was presented again: either a client bug or a stolen token.
            // Kill the whole session family so the attacker (and the victim) must sign in again.
            await RevokeFamilyAsync(current.FamilyId, now, cancellationToken);
            LogTokenReuse(current.WorkerId, current.FamilyId);
            return AuthErrors.InvalidRefreshToken;
        }

        if (!current.IsActive(now))
        {
            return AuthErrors.InvalidRefreshToken;
        }

        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == current.WorkerId, cancellationToken);
        if (worker is null || !worker.IsActive)
        {
            current.Revoke(now);
            await db.SaveChangesAsync(cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        var response = sessions.Rotate(worker, current);
        await db.SaveChangesAsync(cancellationToken);
        return response;
    }

    private async Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var family = await db.RefreshTokens
            .Where(t => t.FamilyId == familyId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in family)
        {
            token.Revoke(now);
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Refresh token reuse detected for worker {WorkerId}, family {FamilyId} revoked")]
    private partial void LogTokenReuse(Guid workerId, Guid familyId);
}
