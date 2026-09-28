using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Abstractions;

/// <summary>
/// Port to the module's persistence. Handlers use EF Core LINQ directly (no generic repository on top of
/// a unit of work that already is one); the concrete context lives in Infrastructure.
/// </summary>
internal interface IWorkforceDbContext
{
    DbSet<Worker> Workers { get; }

    DbSet<RefreshToken> RefreshTokens { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
