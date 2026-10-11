using Kamada.Modules.Workforce.Application;
using Kamada.Modules.Workforce.Domain.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Workforce.Infrastructure.Persistence.Configurations;

internal sealed class WorkerConfiguration : IEntityTypeConfiguration<Worker>
{
    public void Configure(EntityTypeBuilder<Worker> builder)
    {
        builder.ToTable("workers", t => t.HasCheckConstraint("ck_workers_roles_not_empty", "cardinality(roles) > 0"));
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Username).HasMaxLength(WorkerRules.UsernameMaxLength).IsRequired();
        builder.HasIndex(w => w.Username).IsUnique();

        builder.Property(w => w.PasswordHash).HasMaxLength(256).IsRequired();
        builder.Property(w => w.Name).HasMaxLength(WorkerRules.NameMaxLength).IsRequired();
        builder.Property(w => w.Surname).HasMaxLength(WorkerRules.NameMaxLength).IsRequired();
        builder.Property(w => w.PhoneNumber).HasMaxLength(WorkerRules.PhoneNumberMaxLength).IsRequired();
        builder.PrimitiveCollection(w => w.Roles)
            .HasField("_roles")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .IsRequired()
            .ElementType(e => e.HasConversion<string>());
        builder.Property(w => w.IsActive).IsRequired();

        builder.Ignore(w => w.FullName);
        builder.Ignore(w => w.DomainEvents);
    }
}
