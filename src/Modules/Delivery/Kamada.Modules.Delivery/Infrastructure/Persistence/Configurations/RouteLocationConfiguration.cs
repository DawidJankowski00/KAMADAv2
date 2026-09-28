using Kamada.Modules.Delivery.Domain.Locations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Delivery.Infrastructure.Persistence.Configurations;

internal sealed class RouteLocationConfiguration : IEntityTypeConfiguration<RouteLocation>
{
    public void Configure(EntityTypeBuilder<RouteLocation> builder)
    {
        builder.ToTable("route_locations");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Name).HasMaxLength(100).IsRequired();
        builder.Property(l => l.Street).HasMaxLength(200).IsRequired();
        builder.Property(l => l.BuildingNumber).HasMaxLength(20).IsRequired();
        builder.Property(l => l.City).HasMaxLength(100).IsRequired();
        builder.Property(l => l.PostalCode).HasMaxLength(10).IsRequired();
        builder.Property(l => l.Country).HasMaxLength(60).IsRequired();
        builder.HasIndex(l => l.Name).IsUnique();

        builder.Ignore(l => l.Location);
        builder.Ignore(l => l.Details);
        builder.Ignore(l => l.DomainEvents);
    }
}
