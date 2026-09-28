using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Configurations;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("workers");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Username).HasMaxLength(WorkerRules.UsernameMaxLength).IsRequired();
        builder.HasIndex(w => w.Username).IsUnique();

        builder.Property(w => w.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(WorkerRules.NameMaxLength).IsRequired();
        builder.Property(w => w.Surname).HasMaxLength(WorkerRules.NameMaxLength).IsRequired();
        builder.Property(w => w.PhoneNumber).HasMaxLength(WorkerRules.PhoneNumberMaxLength).IsRequired();
        builder.Property(w => w.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(w => w.IsActive).IsRequired();

        builder.Ignore(w => w.FullName);
        builder.Ignore(w => w.DomainEvents);
    }
}
