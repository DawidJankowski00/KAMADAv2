using Kamada.Modules.Ordering.Domain.Orders;
using Kamada.Modules.Ordering.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Ordering.Application.Abstractions;

internal interface IOrderingDbContext
{
    DbSet<Order> Orders { get; }

    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
