using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Modules.Ordering.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments", t => t.HasCheckConstraint("ck_payments_amount_positive", "amount > 0"));
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.Provider).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.TransactionId).HasMaxLength(64).IsRequired();
        builder.HasIndex(p => p.TransactionId).IsUnique();
        builder.Property(p => p.Amount).HasPrecision(10, 2);
        builder.Property(p => p.FailureReason).HasMaxLength(500);

        // Last line of defence against two concurrent "pay" clicks: one active payment per order.
        // Named explicitly: the snake_case convention would otherwise call it ix_payments_order_id1.
        builder.HasIndex(p => p.OrderId, "ux_payments_one_active_per_order")
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Completed')")
            .HasDatabaseName("ux_payments_one_active_per_order");
        builder.HasIndex(p => p.OrderId, "ix_payments_order_id");
        // Same schema since payments moved into Ordering, so the database can enforce the reference.
        // Restrict: an order with payments is never deleted.
        builder.HasOne<Order>()
            .WithMany()
            .HasForeignKey(p => p.OrderId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(p => p.IsActive);
        builder.Ignore(p => p.DomainEvents);
    }
}
