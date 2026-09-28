using Kamada.Modules.Payments.Application.Abstractions;
using Kamada.Modules.Payments.Domain.Payments;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Payments.Infrastructure.Persistence;

internal sealed class PaymentsDbContext(DbContextOptions<PaymentsDbContext> options)
    : DbContext(options), IPaymentsDbContext
{
    public const string Schema = "payments";

    public DbSet<Payment> Payments => Set<Payment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        var payment = modelBuilder.Entity<Payment>();
        payment.ToTable("payments", t => t.HasCheckConstraint("ck_payments_amount_positive", "amount > 0"));
        payment.HasKey(p => p.Id);
        payment.Property(p => p.Id).ValueGeneratedNever();
        payment.Property(p => p.Provider).HasConversion<string>().HasMaxLength(20);
        payment.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        payment.Property(p => p.TransactionId).HasMaxLength(64).IsRequired();
        payment.HasIndex(p => p.TransactionId).IsUnique();
        payment.Property(p => p.Amount).HasPrecision(10, 2);
        payment.Property(p => p.FailureReason).HasMaxLength(500);

        // Last line of defence against two concurrent "pay" clicks: one active payment per order.
        payment.HasIndex(p => p.OrderId, "ux_payments_one_active_per_order")
            .IsUnique()
            .HasFilter("status IN ('Pending', 'Completed')");
        payment.HasIndex(p => p.OrderId, "ix_payments_order_id");

        payment.Ignore(p => p.IsActive);
        payment.Ignore(p => p.DomainEvents);

        modelBuilder.UseXminConcurrencyTokens();
    }
}
