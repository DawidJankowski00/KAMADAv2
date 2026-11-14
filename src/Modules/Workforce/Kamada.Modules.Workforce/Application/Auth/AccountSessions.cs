using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>
/// A worker's own sessions (refresh token families with an active token): list and sign out. Signing a session out
/// revokes its refresh token; an access token already issued to it stays valid until it expires (≤ 15 min).
/// </summary>
internal sealed class AccountSessions(IWorkforceDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<SessionDto>> ListAsync(Guid workerId, Guid? currentSessionId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var sessions = await db.RefreshTokens.AsNoTracking()
            .Where(t => t.WorkerId == workerId && t.RevokedAt == null && t.ExpiresAt > now)
            .Select(t => new
            {
                t.FamilyId,
                LastUsedAt = t.CreatedAt,
                StartedAt = db.RefreshTokens.Where(f => f.FamilyId == t.FamilyId).Min(f => f.CreatedAt),
                t.UserAgent,
                t.IpAddress,
            })
            .ToListAsync(cancellationToken);

        return
        [
            .. sessions
                .Select(s => new SessionDto(s.FamilyId, s.StartedAt, s.LastUsedAt, s.UserAgent, s.IpAddress, s.FamilyId == currentSessionId))
                .OrderByDescending(s => s.IsCurrent)
                .ThenByDescending(s => s.LastUsedAt),
        ];
    }

    public async Task<Result> RevokeAsync(Guid workerId, Guid sessionId, CancellationToken cancellationToken)
    {
        var tokens = await ActiveTokens(workerId).Where(t => t.FamilyId == sessionId).ToListAsync(cancellationToken);
        if (tokens.Count == 0)
        {
            return AuthErrors.SessionNotFound;
        }

        Revoke(tokens);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Signs out every session except the caller's (all of them for a token without a session id).</summary>
    public async Task RevokeOthersAsync(Guid workerId, Guid? currentSessionId, CancellationToken cancellationToken)
    {
        var tokens = await ActiveTokens(workerId).Where(t => t.FamilyId != currentSessionId).ToListAsync(cancellationToken);
        Revoke(tokens);
        await db.SaveChangesAsync(cancellationToken);
    }

    private IQueryable<RefreshToken> ActiveTokens(Guid workerId) =>
        db.RefreshTokens.Where(t => t.WorkerId == workerId && t.RevokedAt == null);

    private void Revoke(IEnumerable<RefreshToken> tokens)
    {
        var now = clock.GetUtcNow();
        foreach (var token in tokens)
        {
            token.Revoke(now);
        }
    }
}
