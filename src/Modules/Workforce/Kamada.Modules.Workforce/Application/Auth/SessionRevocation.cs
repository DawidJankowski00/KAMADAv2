using Kamada.Modules.Workforce.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Auth;

internal static class SessionRevocation
{
    /// <summary>
    /// Revokes every active refresh token of the worker (signs them out everywhere; access tokens expire within
    /// minutes). The caller saves changes.
    /// </summary>
    public static async Task RevokeAllSessionsAsync(
        this IWorkforceDbContext db,
        Guid workerId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var active = await db.RefreshTokens
            .Where(t => t.WorkerId == workerId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }
}
