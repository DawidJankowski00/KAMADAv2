using Kamada.Modules.Customers.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Application.Abstractions;

internal interface ICustomersDbContext
{
    DbSet<Client> Clients { get; }

    DbSet<Address> Addresses { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
