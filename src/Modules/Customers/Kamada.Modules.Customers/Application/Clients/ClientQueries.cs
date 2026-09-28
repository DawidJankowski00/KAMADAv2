using Kamada.Modules.Customers.Application.Abstractions;
using Kamada.Modules.Customers.Domain.Clients;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Customers;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Customers.Application.Clients;

internal sealed class ClientQueries(ICustomersDbContext db)
{
    public async Task<PagedResult<ClientSummaryDto>> ListAsync(ClientListQuery query, CancellationToken cancellationToken)
    {
        var clients = db.Clients.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            clients = clients.Where(c =>
                EF.Functions.ILike(c.Name, pattern) ||
                EF.Functions.ILike(c.Surname, pattern) ||
                EF.Functions.ILike(c.Email, pattern) ||
                EF.Functions.ILike(c.Username, pattern) ||
                EF.Functions.ILike(c.PhoneNumber, pattern));
        }

        if (query.IsActive is { } isActive)
        {
            clients = clients.Where(c => c.IsActive == isActive);
        }

        var total = await clients.CountAsync(cancellationToken);
        var items = await clients
            .OrderBy(c => c.Surname).ThenBy(c => c.Name).ThenBy(c => c.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new ClientSummaryDto(
                c.Id,
                c.Username,
                c.Email,
                c.Name,
                c.Surname,
                c.PhoneNumber,
                c.IsActive,
                c.Addresses.Count(a => !a.IsArchived),
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClientSummaryDto>(items, query.Page, query.PageSize, total);
    }

    public async Task<Result<ClientDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await db.Clients.AsNoTracking()
            .Include(c => c.Addresses)
            .SingleOrDefaultAsync(c => c.Id == id, cancellationToken);

        return client is null ? ClientErrors.NotFound : client.ToDto();
    }
}
