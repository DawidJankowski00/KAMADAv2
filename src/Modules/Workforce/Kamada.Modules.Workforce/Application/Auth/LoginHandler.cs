using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Workforce.Application.Auth;

internal sealed partial class LoginHandler(
    IWorkforceDbContext db,
    IPasswordHasher<Worker> passwordHasher,
    AuthSessionFactory sessions,
    ILogger<LoginHandler> logger)
{
    /// <summary>
    /// Hash verified when the user does not exist, so response time does not reveal valid usernames.
    /// </summary>
    private static readonly Lazy<string> DummyHash =
        new(() => new PasswordHasher<Worker>().HashPassword(null!, Guid.NewGuid().ToString()));

    public async Task<Result<TokenResponse>> HandleAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var username = Worker.NormalizeUsername(request.Username);
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Username == username, cancellationToken);

        if (worker is null)
        {
            passwordHasher.VerifyHashedPassword(null!, DummyHash.Value, request.Password);
            LogFailedLogin(username, "unknown user");
            return AuthErrors.InvalidCredentials;
        }

        var verification = passwordHasher.VerifyHashedPassword(worker, worker.PasswordHash, request.Password);
        if (verification == PasswordVerificationResult.Failed || !worker.IsActive)
        {
            LogFailedLogin(username, worker.IsActive ? "wrong password" : "inactive account");
            return AuthErrors.InvalidCredentials;
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.Password));
        }

        var response = sessions.Start(worker);
        await db.SaveChangesAsync(cancellationToken);

        LogSuccessfulLogin(worker.Id, worker.Role);
        return response;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed login for {Username}: {Reason}")]
    private partial void LogFailedLogin(string username, string reason);

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} ({Role}) signed in")]
    private partial void LogSuccessfulLogin(Guid workerId, WorkerRole role);
}
