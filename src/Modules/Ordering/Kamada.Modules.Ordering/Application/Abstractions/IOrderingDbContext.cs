using Kamada.Modules.Ordering.Domain.Orders;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application.Abstractions;

internal interface IOrderingDbContext
{
    DbSet<Order> Orders { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
