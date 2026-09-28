using Kamada.Modules.Payments.Domain.Payments;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Payments.Application.Abstractions;

internal interface IPaymentsDbContext
{
    DbSet<Payment> Payments { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
