using System.Text.Json;
using Kamada.Modules.Delivery.Domain.Locations;
using Kamada.Modules.Delivery.Domain.Runs;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Delivery.Infrastructure.Persistence.Configurations;

internal sealed class DeliveryRunConfiguration : IEntityTypeConfiguration<DeliveryRun>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public void Configure(EntityTypeBuilder<DeliveryRun> builder)
    {
        builder.ToTable("delivery_runs");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();
        builder.Property(r => r.Name).HasMaxLength(100).IsRequired();
        builder.Property(r => r.VehiclePlate).HasMaxLength(20).IsRequired();
        builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(r => r.OptimizationAlgorithm).HasMaxLength(50);
        builder.Property(r => r.MatrixProvider).HasMaxLength(20);

        // "google_maps_urls" (jsonb): the list of chained links, read and written as a whole.
        builder.Property(r => r.NavigationLinks)
            .HasColumnName("google_maps_urls")
            .HasColumnType("jsonb")
            .HasConversion(
                links => JsonSerializer.Serialize(links, Json),
                json => JsonSerializer.Deserialize<List<RunNavigationLink>>(json, Json) ?? new List<RunNavigationLink>(),
                new ValueComparer<IReadOnlyList<RunNavigationLink>>(
                    (a, b) => a!.SequenceEqual(b!),
                    links => links.Aggregate(0, (hash, link) => HashCode.Combine(hash, link.GetHashCode())),
                    links => links.ToList()));

        builder.HasIndex(r => new { r.CourierId, r.PlannedDate });
        builder.HasIndex(r => new { r.Status, r.PlannedDate });

        builder.HasOne<RouteLocation>().WithMany().HasForeignKey(r => r.StartLocationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RouteLocation>().WithMany().HasForeignKey(r => r.EndLocationId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(r => r.Stops)
            .WithOne()
            .HasForeignKey(s => s.DeliveryRunId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(r => r.Stops).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(r => r.HasRoutePlan);
        builder.Ignore(r => r.DomainEvents);
    }
}
