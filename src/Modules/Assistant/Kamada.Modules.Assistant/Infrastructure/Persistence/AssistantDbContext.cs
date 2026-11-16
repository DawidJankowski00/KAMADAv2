using Kamada.Modules.Assistant.Application.Abstractions;
using Kamada.Modules.Assistant.Domain.Settings;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Assistant.Infrastructure.Persistence;

/// <summary>The assistant's own data: only its runtime settings (notes, drafts and recordings are never stored).</summary>
internal sealed class AssistantDbContext(DbContextOptions<AssistantDbContext> options)
    : DbContext(options), IAssistantDbContext
{
    public const string Schema = "assistant";

    public DbSet<AssistantSettings> Settings => Set<AssistantSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(AssistantDbContext).Assembly,
            t => t.Namespace == typeof(AssistantDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
