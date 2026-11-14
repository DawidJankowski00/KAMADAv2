using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>A signed-in worker turns two-factor authentication on or off for their own account.</summary>
internal sealed partial class TwoFactorSetup(
    IWorkforceDbContext db,
    ITwoFactorService twoFactor,
    TwoFactorCodes codes,
    IPasswordHasher<Worker> passwordHasher,
    TimeProvider clock,
    ILogger<TwoFactorSetup> logger)
{
    public async Task<Result<TwoFactorStatusDto>> GetStatusAsync(Guid workerId, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        var left = await db.RecoveryCodes.CountAsync(c => c.WorkerId == workerId && c.UsedAt == null, cancellationToken);
        return new TwoFactorStatusDto(worker.TwoFactorEnabled, left);
    }

    /// <summary>Creates a new secret for the authenticator app; 2FA is on only after <see cref="EnableAsync"/>.</summary>
    public async Task<Result<TwoFactorSetupDto>> BeginAsync(Guid workerId, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        var key = twoFactor.CreateKey();
        var started = worker.BeginTwoFactorSetup(key.ProtectedSecret);
        if (started.IsFailure)
        {
            return started.Error;
        }

        await db.SaveChangesAsync(cancellationToken);
        return new TwoFactorSetupDto(twoFactor.FormatSharedKey(key.SharedKey), twoFactor.AuthenticatorUri(worker.Username, key.SharedKey));
    }

    /// <summary>Confirms the pending secret with a code from the app, turns 2FA on and returns the recovery codes.</summary>
    public async Task<Result<RecoveryCodesDto>> EnableAsync(Guid workerId, TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        if (worker.TwoFactorEnabled)
        {
            return WorkerErrors.TwoFactorAlreadyEnabled;
        }

        if (worker.PendingTwoFactorSecret is null)
        {
            return WorkerErrors.TwoFactorNotSetUp;
        }

        if (twoFactor.VerifyCode(worker.PendingTwoFactorSecret, request.Code, clock.GetUtcNow()) is not { } step)
        {
            return AuthErrors.InvalidTwoFactorCode;
        }

        var enabled = worker.EnableTwoFactor(step);
        if (enabled.IsFailure)
        {
            return enabled.Error;
        }

        var recoveryCodes = await codes.ReplaceRecoveryCodesAsync(worker.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        LogEnabled(worker.Id);
        return new RecoveryCodesDto(recoveryCodes);
    }

    /// <summary>Needs the password and a valid code, so neither a stolen session nor a stolen password alone turns 2FA off.</summary>
    public async Task<Result> DisableAsync(Guid workerId, DisableTwoFactorRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        if (!worker.TwoFactorEnabled)
        {
            return WorkerErrors.TwoFactorNotEnabled;
        }

        if (passwordHasher.VerifyHashedPassword(worker, worker.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return AuthErrors.CurrentPasswordInvalid;
        }

        if (!await codes.AcceptAsync(worker, request.Code, cancellationToken))
        {
            return AuthErrors.InvalidTwoFactorCode;
        }

        worker.DisableTwoFactor();
        await codes.RemoveRecoveryCodesAsync(worker.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        LogDisabled(worker.Id);
        return Result.Success();
    }

    /// <summary>A new set of recovery codes (the old ones stop working); needs a valid code.</summary>
    public async Task<Result<RecoveryCodesDto>> RegenerateRecoveryCodesAsync(Guid workerId, TwoFactorCodeRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        if (!worker.TwoFactorEnabled)
        {
            return WorkerErrors.TwoFactorNotEnabled;
        }

        if (!await codes.AcceptAsync(worker, request.Code, cancellationToken))
        {
            return AuthErrors.InvalidTwoFactorCode;
        }

        var recoveryCodes = await codes.ReplaceRecoveryCodesAsync(worker.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return new RecoveryCodesDto(recoveryCodes);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Worker {WorkerId} enabled two-factor authentication")]
    private partial void LogEnabled(Guid workerId);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Worker {WorkerId} disabled two-factor authentication")]
    private partial void LogDisabled(Guid workerId);
}
