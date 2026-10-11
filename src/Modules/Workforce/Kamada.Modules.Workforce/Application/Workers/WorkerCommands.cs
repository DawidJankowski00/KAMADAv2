using Kamada.Modules.Workforce.Application.Abstractions;
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

        var created = Worker.Create(
            username,
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

        var roleChange = worker.SetRoles([.. request.Roles.Select(r => r.ToDomain())], currentUser.Id);
        if (roleChange.IsFailure)
        {
            return roleChange.Error;
        }

        worker.UpdateProfile(request.Name, request.Surname, request.PhoneNumber);
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
        await RevokeSessionsAsync(worker.Id, cancellationToken);
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

        await RevokeSessionsAsync(worker.Id, cancellationToken);
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

    /// <summary>Signs the worker out everywhere (their access tokens expire within minutes).</summary>
    private async Task RevokeSessionsAsync(Guid workerId, CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var active = await db.RefreshTokens
            .Where(t => t.WorkerId == workerId && t.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in active)
        {
            token.Revoke(now);
        }
    }
}
