using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Contracts;

public sealed record CourierInfo(Guid Id, string FullName, string PhoneNumber, bool IsActive);

/// <summary>What other modules (Delivery) need to know about couriers.</summary>
public interface ICourierDirectory
{
    /// <summary>Fails unless the worker exists, has the Courier role and is active.</summary>
    Task<Result<CourierInfo>> GetAssignableCourierAsync(Guid workerId, CancellationToken cancellationToken);

    /// <summary>Couriers by id (inactive ones included); unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, CourierInfo>> GetCouriersAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken);
}
