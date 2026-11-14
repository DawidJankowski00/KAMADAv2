using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence;

internal sealed class WorkforceDbContext(DbContextOptions<WorkforceDbContext> options)
    : DbContext(options), IWorkforceDbContext, IDataProtectionKeyContext
{
    public const string Schema = "workforce";

    public DbSet<Worker> Workers => Set<Worker>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    /// <summary>
    /// ASP.NET Core Data Protection key ring (encrypts the 2FA secrets). Kept here because Workforce owns the data it
    /// protects; shared by every API instance, so a restart or a second instance can still decrypt.
    /// </summary>
    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(WorkforceDbContext).Assembly, t => t.Namespace == typeof(WorkforceDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
