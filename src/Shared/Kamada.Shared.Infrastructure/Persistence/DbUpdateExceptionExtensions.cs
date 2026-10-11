using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Kamada.Shared.Infrastructure.Persistence;

public static class DbUpdateExceptionExtensions
{
    /// <summary>
    /// True when the save failed on a unique index, e.g. because two identical retries of an idempotent create raced each
    /// other and the other one was saved first. Which index Postgres reports first is not defined, so callers look up
    /// the record by its request id instead of relying on the index name.
    /// </summary>
    public static bool IsUniqueViolation(this DbUpdateException exception) =>
        exception.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
