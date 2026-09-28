using Kamada.Modules.Customers.Application.Clients;
using Kamada.Modules.Customers.Domain.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kamada.Modules.Customers.Infrastructure.Persistence.Configurations;

internal sealed class ClientConfiguration : IEntityTypeConfiguration<Client>
{
    public void Configure(EntityTypeBuilder<Client> builder)
    {
        builder.ToTable("clients");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Username).HasMaxLength(CustomerRules.UsernameMaxLength).IsRequired();
        builder.HasIndex(c => c.Username).IsUnique();
        builder.Property(c => c.Email).HasMaxLength(CustomerRules.EmailMaxLength).IsRequired();
        builder.HasIndex(c => c.Email).IsUnique();
        builder.Property(c => c.Name).HasMaxLength(CustomerRules.NameMaxLength).IsRequired();
        builder.Property(c => c.Surname).HasMaxLength(CustomerRules.NameMaxLength).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(CustomerRules.PhoneNumberMaxLength).IsRequired();

        builder.HasMany(c => c.Addresses)
            .WithOne()
            .HasForeignKey(a => a.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Navigation(c => c.Addresses).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(c => c.ActiveAddresses);
        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        builder.ToTable("addresses", t => t.HasCheckConstraint(
            "ck_addresses_coordinates_both_or_none",
            "(latitude IS NULL) = (longitude IS NULL)"));
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.Street).HasMaxLength(CustomerRules.StreetMaxLength).IsRequired();
        builder.Property(a => a.BuildingNumber).HasMaxLength(CustomerRules.BuildingNumberMaxLength).IsRequired();
        builder.Property(a => a.ApartmentNumber).HasMaxLength(CustomerRules.BuildingNumberMaxLength);
        builder.Property(a => a.City).HasMaxLength(CustomerRules.CityMaxLength).IsRequired();
        builder.Property(a => a.PostalCode).HasMaxLength(CustomerRules.PostalCodeMaxLength).IsRequired();
        builder.Property(a => a.Country).HasMaxLength(CustomerRules.CountryMaxLength).IsRequired();
        builder.Property(a => a.CoordinatesSource).HasConversion<string>().HasMaxLength(20);

        builder.Ignore(a => a.Location);
        builder.Ignore(a => a.Details);
        builder.Ignore(a => a.DomainEvents);
    }
}
