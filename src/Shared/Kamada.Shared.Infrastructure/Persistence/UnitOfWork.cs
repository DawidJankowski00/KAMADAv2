using System.Data;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Shared.Infrastructure.Persistence;

/// <summary>
/// Runs work spanning several modules atomically. Each module still saves its own <c>DbContext</c>;
/// all of them are enlisted in one database transaction that is committed only when the work returns success.
/// </summary>
public interface IUnitOfWork
{
    Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken);

    Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken);
}

internal sealed class UnitOfWork(ScopedDbConnection connection, IEnumerable<IModuleDatabase> databases) : IUnitOfWork
{
    private bool _inTransaction;

    public async Task<Result<T>> ExecuteAsync<T>(Func<CancellationToken, Task<Result<T>>> work, CancellationToken cancellationToken)
    {
        Result<T>? result = null;
        await RunInTransactionAsync(
            async ct =>
            {
                result = await work(ct);
                return result;
            },
            cancellationToken);
        return result!;
    }

    public Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken) =>
        RunInTransactionAsync(work, cancellationToken);

    private async Task<Result> RunInTransactionAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken)
    {
        // Nested call: join the transaction that is already running.
        if (_inTransaction)
        {
            return await work(cancellationToken);
        }

        var db = connection.Connection;
        if (db.State != ConnectionState.Open)
        {
            await db.OpenAsync(cancellationToken);
        }

        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var contexts = databases.Select(d => d.Context).ToList();
        foreach (var context in contexts)
        {
            await context.Database.UseTransactionAsync(transaction, cancellationToken);
        }

        _inTransaction = true;
        try
        {
            var result = await work(cancellationToken);
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            _inTransaction = false;
            foreach (var context in contexts)
            {
                await context.Database.UseTransactionAsync(null, CancellationToken.None);
            }
        }
    }
}
