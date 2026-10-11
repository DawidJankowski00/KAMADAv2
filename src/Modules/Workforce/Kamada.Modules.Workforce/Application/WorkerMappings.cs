using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Workforce;
using ContractRole = Kamada.Shared.Contracts.Workforce.WorkerRole;
using DomainRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.Modules.Workforce.Application;

internal static class WorkerMappings
{
    public static WorkerDto ToDto(this Worker worker) => new(
        worker.Id,
        worker.Username,
        worker.Name,
        worker.Surname,
        worker.PhoneNumber,
        [.. worker.Roles.Select(r => r.ToContract())],
        worker.IsActive,
        worker.CreatedAt);

    public static AuthenticatedUserDto ToAuthenticatedUser(this Worker worker) =>
        new(worker.Id, worker.Username, worker.Name, worker.Surname, [.. worker.Roles.Select(r => r.ToRoleName())]);

    public static ContractRole ToContract(this DomainRole role) => role switch
    {
        DomainRole.Admin => ContractRole.Admin,
        DomainRole.Courier => ContractRole.Courier,
        DomainRole.Seller => ContractRole.Seller,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    public static DomainRole ToDomain(this ContractRole role) => role switch
    {
        ContractRole.Admin => DomainRole.Admin,
        ContractRole.Courier => DomainRole.Courier,
        ContractRole.Seller => DomainRole.Seller,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };

    /// <summary>Role name used in the JWT <c>role</c> claim.</summary>
    public static string ToRoleName(this DomainRole role) => role switch
    {
        DomainRole.Admin => Roles.Admin,
        DomainRole.Courier => Roles.Courier,
        DomainRole.Seller => Roles.Seller,
        _ => throw new ArgumentOutOfRangeException(nameof(role), role, null),
    };
}
