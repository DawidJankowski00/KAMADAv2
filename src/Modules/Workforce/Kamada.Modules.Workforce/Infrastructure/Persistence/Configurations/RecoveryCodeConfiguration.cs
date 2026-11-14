using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Configurations;

internal sealed class RecoveryCodeConfiguration : IEntityTypeConfiguration<RecoveryCode>
{
    public void Configure(EntityTypeBuilder<RecoveryCode> builder)
    {
        builder.ToTable("recovery_codes");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();
        builder.Property(c => c.CodeHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(c => new { c.WorkerId, c.CodeHash }).IsUnique();
        builder.HasOne<Worker>()
            .WithMany()
            .HasForeignKey(c => c.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(c => c.DomainEvents);
    }
}
