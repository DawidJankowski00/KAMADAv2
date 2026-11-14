using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Workforce;
using Kamada.Shared.Infrastructure.Auth;
using Kamada.Shared.Kernel.Results;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Workers;

/// <summary>
/// Administrative use cases on worker accounts.
/// </summary>
internal sealed class WorkerCommands(
    IWorkforceDbContext db,
    IPasswordHasher<Worker> passwordHasher,
    ICurrentUser currentUser,
    TimeProvider clock)
{
    public async Task<Result<WorkerDto>> CreateAsync(CreateWorkerRequest request, CancellationToken cancellationToken)
    {
        var username = Worker.NormalizeUsername(request.Username);
        if (await db.Workers.AnyAsync(w => w.Username == username, cancellationToken))
        {
            return WorkerErrors.UsernameTaken;
        }

        if (await EmailTakenAsync(request.Email, workerId: null, cancellationToken))
        {
            return WorkerErrors.EmailTaken;
        }

        var created = Worker.Create(
            username,
            request.Email,
            passwordHash: string.Empty,
            request.Name,
            request.Surname,
            request.PhoneNumber,
            [.. request.Roles.Select(r => r.ToDomain())]);
        if (created.IsFailure)
        {
            return created.Error;
        }

        var worker = created.Value;
        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.Password));

        db.Workers.Add(worker);
        await db.SaveChangesAsync(cancellationToken);
        return worker.ToDto();
    }

    public async Task<Result<WorkerDto>> UpdateAsync(Guid id, UpdateWorkerRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        if (await EmailTakenAsync(request.Email, worker.Id, cancellationToken))
        {
            return WorkerErrors.EmailTaken;
        }

        var roleChange = worker.SetRoles([.. request.Roles.Select(r => r.ToDomain())], currentUser.Id);
        if (roleChange.IsFailure)
        {
            return roleChange.Error;
        }

        worker.UpdateProfile(request.Email, request.Name, request.Surname, request.PhoneNumber);
        await db.SaveChangesAsync(cancellationToken);
        return worker.ToDto();
    }

    public async Task<Result> ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        worker.ChangePasswordHash(passwordHasher.HashPassword(worker, request.NewPassword));
        worker.ClearFailedLogins();
        await db.RevokeAllSessionsAsync(worker.Id, clock.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>
    /// Turns 2FA off for a worker who lost both the phone and the recovery codes, and signs them out everywhere; they
    /// sign in with the password and may set 2FA up again.
    /// </summary>
    public async Task<Result> ResetTwoFactorAsync(Guid id, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        worker.DisableTwoFactor();
        db.RecoveryCodes.RemoveRange(await db.RecoveryCodes.Where(c => c.WorkerId == id).ToListAsync(cancellationToken));
        await db.RevokeAllSessionsAsync(worker.Id, clock.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    /// <summary>Lifts a lockout after repeated wrong passwords (the worker may sign in again at once).</summary>
    public async Task<Result> UnlockAsync(Guid id, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        worker.ClearFailedLogins();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> DeactivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        var result = worker.Deactivate(currentUser.Id);
        if (result.IsFailure)
        {
            return result;
        }

        await db.RevokeAllSessionsAsync(worker.Id, clock.GetUtcNow(), cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    public async Task<Result> ActivateAsync(Guid id, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        if (worker is null)
        {
            return WorkerErrors.NotFound;
        }

        worker.Activate();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }

    private Task<bool> EmailTakenAsync(string email, Guid? workerId, CancellationToken cancellationToken)
    {
        var normalized = Worker.NormalizeEmail(email);
        return db.Workers.AnyAsync(w => w.Email == normalized && w.Id != workerId, cancellationToken);
    }
}
