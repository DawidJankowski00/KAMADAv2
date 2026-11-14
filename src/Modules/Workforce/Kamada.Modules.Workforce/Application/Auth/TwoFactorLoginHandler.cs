using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>
/// Second step of a sign-in with 2FA: the challenge from the password step plus an authenticator or recovery code.
/// Wrong codes count towards the account lockout like wrong passwords, so the 10⁶ codes cannot be tried one by one.
/// </summary>
internal sealed partial class TwoFactorLoginHandler(
    IWorkforceDbContext db,
    ITwoFactorService twoFactor,
    TwoFactorCodes codes,
    AuthSessionFactory sessions,
    IOptions<AccountLockoutOptions> lockout,
    TimeProvider clock,
    ILogger<TwoFactorLoginHandler> logger)
{
    public async Task<Result<TokenResponse>> HandleAsync(TwoFactorLoginRequest request, SessionClient client, CancellationToken cancellationToken)
    {
        if (twoFactor.ReadChallenge(request.ChallengeToken) is not { } workerId)
        {
            return AuthErrors.InvalidTwoFactorLogin;
        }

        var now = clock.GetUtcNow();
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null || !worker.IsActive || !worker.TwoFactorEnabled || worker.IsLockedOut(now))
        {
            return AuthErrors.InvalidTwoFactorLogin;
        }

        if (!await codes.AcceptAsync(worker, request.Code, cancellationToken))
        {
            LogWrongCode(worker.Id);
            worker.RegisterFailedLogin(now, lockout.Value.MaxFailedAttempts, lockout.Value.Duration);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Parallel attempts: one is not counted; the answer stays 401.
            }

            return AuthErrors.InvalidTwoFactorLogin;
        }

        worker.ClearFailedLogins();
        var response = sessions.Start(worker, client);
        await db.SaveChangesAsync(cancellationToken);
        LogSignedIn(worker.Id);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Wrong two-factor code for worker {WorkerId}")]
    private partial void LogWrongCode(Guid workerId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} signed in with two-factor authentication")]
    private partial void LogSignedIn(Guid workerId);
}
