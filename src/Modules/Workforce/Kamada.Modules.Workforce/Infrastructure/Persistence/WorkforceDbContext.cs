using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence;

internal sealed class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options)
    : DbContext(options), IWorkforceDbContext
{
    public const string Schema = "workforce";

    public DbSet<Worker> Workers => Set<Worker>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkforceDbContext).Assembly, t => t.Namespace == typeof(WorkforceDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
