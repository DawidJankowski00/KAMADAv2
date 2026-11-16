using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Auth;

/// <summary>Checks a second-factor code: an authenticator code (once per time step) or an unused recovery code.</summary>
internal sealed class TwoFactorCodes(IWorkforceDbContext db, ITwoFactorService twoFactor, TimeProvider clock)
{
    public const int RecoveryCodeCount = 10;

    /// <summary>True when the code is valid; a recovery code is spent. The caller saves changes.</summary>
    public async Task<bool> AcceptAsync(Worker worker, string code, CancellationToken cancellationToken)
    {
        if (!worker.TwoFactorEnabled || worker.TwoFactorSecret is null)
        {
            return false;
        }

        var now = clock.GetUtcNow();
        if (twoFactor.VerifyCode(worker.TwoFactorSecret, code, now) is { } step)
        {
            return worker.AcceptTwoFactorStep(step);
        }

        var hash = twoFactor.HashRecoveryCode(code);
        var recovery = await db.RecoveryCodes.SingleOrDefaultAsync(
            c => c.WorkerId == worker.Id && c.CodeHash == hash && c.UsedAt == null,
            cancellationToken);
        recovery?.Use(now);
        return recovery is not null;
    }

    /// <summary>Replaces the worker's recovery codes with a new set and returns the codes (shown once). The caller saves.</summary>
    public async Task<IReadOnlyList<string>> ReplaceRecoveryCodesAsync(Guid workerId, CancellationToken cancellationToken)
    {
        await RemoveRecoveryCodesAsync(workerId, cancellationToken);
        var codes = twoFactor.CreateRecoveryCodes(RecoveryCodeCount);
        db.RecoveryCodes.AddRange(codes.Select(code => RecoveryCode.Create(workerId, twoFactor.HashRecoveryCode(code))));
        return codes;
    }

    public async Task RemoveRecoveryCodesAsync(Guid workerId, CancellationToken cancellationToken) =>
        db.RecoveryCodes.RemoveRange(await db.RecoveryCodes.Where(c => c.WorkerId == workerId).ToListAsync(cancellationToken));
}
