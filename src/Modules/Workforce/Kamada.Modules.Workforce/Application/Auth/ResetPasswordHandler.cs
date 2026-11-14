using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>
/// Sets a new password with a token from the reset e-mail. The token is spent, and every session of the worker ends
/// (whoever knew the old password is signed out).
/// </summary>
internal sealed partial class ResetPasswordHandler(
    IWorkforceDbContext db,
    ITokenService tokens,
    IPasswordHasher<Worker> passwordHasher,
    TimeProvider clock,
    ILogger<ResetPasswordHandler> logger)
{
    public async Task<Result> HandleAsync(CompletePasswordResetRequest request, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var hash = tokens.HashOpaqueToken(request.Token);
        var token = await db.PasswordResetTokens.SingleOrDefaultAsync(t => t.TokenHash == hash, cancellationToken);
        if (token is null || !token.IsUsable(now))
        {
            return AuthErrors.InvalidResetToken;
        }

        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == token.WorkerId && w.IsActive, cancellationToken);
        if (worker is null)
        {
            return AuthErrors.InvalidResetToken;
        }

        token.Invalidate(now);
        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.NewPassword));
        await db.RevokeAllSessionsAsync(worker.Id, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        LogPasswordReset(worker.Id);
        return Result.Success();
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} set a new password with a reset link; sessions revoked")]
    private partial void LogPasswordReset(Guid workerId);
}
