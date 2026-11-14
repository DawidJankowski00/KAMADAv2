using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.Application.Auth;

internal sealed partial class LoginHandler(
    IWorkforceDbContext db,
    IPasswordHasher<Worker> passwordHasher,
    AuthSessionFactory sessions,
    IOptions<AccountLockoutOptions> lockout,
    TimeProvider clock,
    ILogger<LoginHandler> logger)
{
    /// <summary>
    /// Hash verified when the user does not exist or is locked out, so response time does not reveal either.
    /// </summary>
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<Worker>().HashPassword(null!, Guid.NewGuid().ToString()));

    public async Task<Result<TokenResponse>> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = Worker.NormalizeUsername(request.Username);
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Username == username, cancellationToken);
        var now = clock.GetUtcNow();

        if (worker is null)
        {
            passwordHasher.VerifyHashedPassword(null!, DummyHash.Value, request.Password);
            LogFailedLogin(username, "unknown user");
            return AuthErrors.InvalidCredentials;
        }

        // Same answer as a wrong password: the response does not tell an attacker that the account exists and is locked.
        if (worker.IsLockedOut(now))
        {
            passwordHasher.VerifyHashedPassword(null!, DummyHash.Value, request.Password);
            LogFailedLogin(username, "locked out");
            return AuthErrors.InvalidCredentials;
        }

        var verification = passwordHasher.VerifyHashedPassword(worker, worker.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed || !worker.IsActive)
        {
            LogFailedLogin(username, worker.IsActive ? "wrong password" : "inactive account");
            if (worker.IsActive)
            {
                await CountFailureAsync(worker, now, cancellationToken);
            }

            return AuthErrors.InvalidCredentials;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.Password));
        }

        worker.ClearFailedLogins();
        var response = sessions.Start(worker);
        await db.SaveChangesAsync(cancellationToken);

        LogSuccessfulLogin(worker.Id, string.Join(", ", worker.Roles));
        return response;
    }

    private async Task CountFailureAsync(Worker worker, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var settings = lockout.Value;
        worker.RegisterFailedLogin(now, settings.MaxFailedAttempts, settings.Duration);
        if (worker.IsLockedOut(now))
        {
            LogLockedOut(worker.Id, worker.LockedUntil!.Value);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Parallel wrong passwords for one account: one of them is not counted. The per-IP rate limit still applies,
            // and the answer must stay "invalid credentials", not a 409.
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login for {Username}: {Reason}")]
    private partial void LogFailedLogin(string username, string reason);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Worker {WorkerId} locked out until {LockedUntil} after repeated failed logins")]
    private partial void LogLockedOut(Guid workerId, DateTimeOffset lockedUntil);

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} ({Roles}) signed in")]
    private partial void LogSuccessfulLogin(Guid workerId, string roles);
}
