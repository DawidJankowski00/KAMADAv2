using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application;

internal sealed class WorkerDirectory(IWorkforceDbContext db) : IWorkerDirectory
{
    public async Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken)
    {
        if (workerIds.Count == 0)
        {
            return new Dictionary<Guid, string>();
        }

        var ids = workerIds.Distinct().ToList();
        var workers = await db.Workers.AsNoTracking()
            .Where(w => ids.Contains(w.Id))
            .Select(w => new { w.Id, w.Name, w.Surname })
            .ToListAsync(cancellationToken);
        return workers.ToDictionary(w => w.Id, w => $"{w.Name} {w.Surname}");
    }
}
