namespace Kamada.Modules.Workforce.Contracts;

/// <summary>What other modules need to show about any worker (e.g. who placed an order).</summary>
public interface IWorkerDirectory
{
    /// <summary>Full names by worker id (inactive workers included); unknown ids are left out.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetNamesAsync(IReadOnlyCollection<Guid> workerIds, CancellationToken cancellationToken);
}
