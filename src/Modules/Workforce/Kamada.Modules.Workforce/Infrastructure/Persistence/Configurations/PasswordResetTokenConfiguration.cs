using Kamada.Modules.Workforce.Domain.Auth;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Configurations;

internal sealed class PasswordResetTokenConfiguration : IEntityTypeConfiguration<PasswordResetToken>
{
    public void Configure(EntityTypeBuilder<PasswordResetToken> builder)
    {
        builder.ToTable("password_reset_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();
        builder.Property(t => t.TokenHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(t => t.TokenHash).IsUnique();
        builder.HasOne<Worker>()
            .WithMany()
            .HasForeignKey(t => t.WorkerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Ignore(t => t.DomainEvents);
    }
}
