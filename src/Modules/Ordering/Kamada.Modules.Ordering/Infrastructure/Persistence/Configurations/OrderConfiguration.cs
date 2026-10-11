using Kamada.Modules.Ordering.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Ordering.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders", t => t.HasCheckConstraint("ck_orders_total_non_negative", "total_amount >= 0"));
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.Property(o => o.Number).UseIdentityAlwaysColumn();
        builder.HasIndex(o => o.Number).IsUnique();

        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(o => o.TotalAmount).HasPrecision(10, 2);
        builder.Property(o => o.Notes).HasMaxLength(1000);
        builder.HasIndex(o => o.ClientId);
        builder.HasIndex(o => new { o.PlacedByWorkerId, o.CreatedAt });
        builder.HasIndex(o => new { o.Status, o.CreatedAt });

        // Snapshot stored in the orders row: shipping_address_id (diagram column) + shipping_* copies.
        builder.ComplexProperty(o => o.ShippingAddress, address =>
        {
            address.Property(a => a.AddressId).HasColumnName("shipping_address_id");
            address.Property(a => a.RecipientName).HasColumnName("shipping_recipient_name").HasMaxLength(201);
            address.Property(a => a.PhoneNumber).HasColumnName("shipping_phone_number").HasMaxLength(20);
            address.Property(a => a.Street).HasColumnName("shipping_street").HasMaxLength(200);
            address.Property(a => a.BuildingNumber).HasColumnName("shipping_building_number").HasMaxLength(20);
            address.Property(a => a.ApartmentNumber).HasColumnName("shipping_apartment_number").HasMaxLength(20);
            address.Property(a => a.City).HasColumnName("shipping_city").HasMaxLength(100);
            address.Property(a => a.PostalCode).HasColumnName("shipping_postal_code").HasMaxLength(10);
            address.Property(a => a.Country).HasColumnName("shipping_country").HasMaxLength(60);
            address.Property(a => a.Latitude).HasColumnName("shipping_latitude");
            address.Property(a => a.Longitude).HasColumnName("shipping_longitude");
        });

        builder.HasMany(o => o.Items)
            .WithOne()
            .HasForeignKey(i => i.OrderId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(o => o.Items).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(o => o.DomainEvents);
    }
}

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("order_items", t =>
        {
            t.HasCheckConstraint("ck_order_items_quantity_positive", "quantity > 0");
            t.HasCheckConstraint("ck_order_items_unit_price_positive", "unit_price > 0");
        });
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();
        builder.Property(i => i.ProductName).HasMaxLength(200).IsRequired();
        builder.Property(i => i.Sku).HasMaxLength(40).IsRequired();
        builder.Property(i => i.UnitPrice).HasPrecision(10, 2);
        builder.HasIndex(i => i.ProductId);

        builder.Ignore(i => i.LineTotal);
        builder.Ignore(i => i.DomainEvents);
    }
}
