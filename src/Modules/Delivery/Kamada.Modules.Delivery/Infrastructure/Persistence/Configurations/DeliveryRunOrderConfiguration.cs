using Kamada.Modules.Delivery.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryRunOrderConfiguration : IEntityTypeConfiguration<DeliveryRunOrder>
{
    public void Configure(EntityTypeBuilder<DeliveryRunOrder> builder)
    {
        builder.ToTable("delivery_run_orders", t => t.HasCheckConstraint("ck_delivery_run_orders_sequence_positive", "delivery_sequence > 0"));
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.FailureReason).HasMaxLength(500);
        builder.HasIndex(s => s.OrderId);

        // (delivery_run_id, delivery_sequence) is unique through a DEFERRABLE constraint created in the migration
        // (its index also serves lookups by run): reordering swaps sequences row by row, which holds only at commit.

        builder.Ignore(s => s.PlannedLocation);
        builder.Ignore(s => s.IsFinal);
        builder.Ignore(s => s.DomainEvents);
    }
}
