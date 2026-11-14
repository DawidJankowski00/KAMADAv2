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
/// A signed-in worker changes their own password. Requires the current password (a stolen access token alone is not
/// enough), signs the worker out of every other session and returns a new token pair for the current one.
/// </summary>
internal sealed partial class ChangePasswordHandler(
    IWorkforceDbContext db,
    IPasswordHasher<Worker> passwordHasher,
    AuthSessionFactory sessions,
    TimeProvider clock,
    ILogger<ChangePasswordHandler> logger)
{
    public async Task<Result<TokenResponse>> HandleAsync(Guid workerId, ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId && w.IsActive, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        if (passwordHasher.VerifyHashedPassword(worker, worker.PasswordHash, request.CurrentPassword) == PasswordVerificationResult.Failed)
        {
            LogWrongCurrentPassword(worker.Id);
            return AuthErrors.CurrentPasswordInvalid;
        }

        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.NewPassword));
        await db.RevokeAllSessionsAsync(worker.Id, clock.GetUtcNow(), cancellationToken);
        var response = sessions.Start(worker);
        await db.SaveChangesAsync(cancellationToken);

        LogPasswordChanged(worker.Id);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Worker {WorkerId} failed to change the password: wrong current password")]
    private partial void LogWrongCurrentPassword(Guid workerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} changed the password; other sessions revoked")]
    private partial void LogPasswordChanged(Guid workerId);
}
