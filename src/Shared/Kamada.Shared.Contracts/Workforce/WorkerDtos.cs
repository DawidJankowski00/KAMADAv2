namespace Kamada.Shared.Contracts.Workforce;

public enum WorkerRole
{
    Admin,
    Courier,
    Seller,
}

/// <param name="Roles">One or more roles, in enum order.</param>
public sealed record WorkerDto(
    Guid Id,
    string Username,
    string Name,
    string Surname,
    string PhoneNumber,
    IReadOnlyList<WorkerRole> Roles,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkerRequest(
    string Username,
    string Password,
    string Name,
    string Surname,
    string PhoneNumber,
    IReadOnlyList<WorkerRole> Roles);

/// <param name="Roles">Replaces all roles; a worker cannot remove the Admin role from their own account.</param>
public sealed record UpdateWorkerRequest(string Name, string Surname, string PhoneNumber, IReadOnlyList<WorkerRole> Roles);

public sealed record ResetPasswordRequest(string NewPassword);

/// <param name="Role">Only workers who have this role (among others).</param>
public sealed record WorkerListQuery(
    string? Search = null,
    WorkerRole? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);
