using Kamada.Modules.Customers.Application.Abstractions;
using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Infrastructure.Persistence;

internal sealed class CustomersDbContext(DbContextOptions<CustomersDbContext> options)
    : DbContext(options), ICustomersDbContext
{
    public const string Schema = "customers";

    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Address> Addresses => Set<Address>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(CustomersDbContext).Assembly,
            t => t.Namespace == typeof(CustomersDbContext).Namespace + ".Configurations");
        modelBuilder.UseXminConcurrencyTokens();
    }
}
