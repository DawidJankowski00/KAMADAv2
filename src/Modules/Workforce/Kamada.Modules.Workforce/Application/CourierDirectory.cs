using Kamada.Modules.Workforce.Application.Abstractions;
using Kamada.Modules.Workforce.Contracts;
using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Kernel.Results;
using Microsoft.EntityFrameworkCore;

namespace Kamada.Modules.Workforce.Application;

/// <summary>Public read API of the Workforce module.</summary>
internal sealed class CourierDirectory(IWorkforceDbContext db) : ICourierDirectory
{
    public static readonly Error CourierNotFound =
        Error.Validation("Workers.CourierNotFound", "The courier does not exist.");

    public static readonly Error NotACourier =
        Error.Validation("Workers.NotACourier", "The selected worker is not a courier.");

    public static readonly Error CourierInactive =
        Error.Conflict("Workers.CourierInactive", "The courier is deactivated.");

    public async Task<Result<CourierInfo>> GetAssignableCourierAsync(Guid workerId, CancellationToken cancellationToken)
    {
        var worker = await db.Workers.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workerId, cancellationToken);
        if (worker is null)
        {
            return CourierNotFound;
        }

        if (!worker.HasRole(WorkerRole.Courier))
        {
            return NotACourier;
        }

        return worker.IsActive ? ToInfo(worker) : CourierInactive;
    }

    public async Task<IReadOnlyDictionary<Guid, CourierInfo>> GetCouriersAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken)
    {
        var ids = workerIds.Distinct().ToList();
        var workers = await db.Workers.AsNoTracking().Where(w => ids.Contains(w.Id)).ToListAsync(cancellationToken);
        return workers.ToDictionary(w => w.Id, ToInfo);
    }

    private static CourierInfo ToInfo(Worker worker) => new(worker.Id, worker.FullName, worker.PhoneNumber, worker.IsActive);
}
