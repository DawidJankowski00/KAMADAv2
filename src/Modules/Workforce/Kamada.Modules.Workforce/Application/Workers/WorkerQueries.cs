using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Common;
using Kamada.Shared.Contracts.Workforce;
using Kamada.Shared.Infrastructure.Persistence;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application.Workers;

internal sealed class WorkerQueries(IWorkforceDbContext db)
{
    public async Task<PagedResult<WorkerDto>> ListAsync(WorkerListQuery query, CancellationToken cancellationToken)
    {
        var workers = db.Workers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = LikePattern.Contains(query.Search);
            workers = workers.Where(w =>
                EF.Functions.ILike(w.Username, pattern) ||
                EF.Functions.ILike(w.Name, pattern) ||
                EF.Functions.ILike(w.Surname, pattern));
        }

        if (query.Role is { } role)
        {
            var domainRole = role.ToDomain();
            workers = workers.Where(w => w.Role == domainRole);
        }

        if (query.IsActive is { } isActive)
        {
            workers = workers.Where(w => w.IsActive == isActive);
        }

        var total = await workers.CountAsync(cancellationToken);
        var page = await workers
            .OrderBy(w => w.Surname).ThenBy(w => w.Name).ThenBy(w => w.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<WorkerDto>([.. page.Select(w => w.ToDto())], query.Page, query.PageSize, total);
    }

    public async Task<Result<WorkerDto>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == id, cancellationToken);
        return worker is null ? WorkerErrors.NotFound : worker.ToDto();
    }
}
