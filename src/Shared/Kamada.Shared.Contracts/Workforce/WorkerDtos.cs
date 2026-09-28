namespace Kamada.Shared.Contracts.Workforce;

public enum WorkerRole
{
    Admin,
    Courier,
}

public sealed record WorkerDto(
    Guid Id,
    string Username,
    string Name,
    string Surname,
    string PhoneNumber,
    WorkerRole Role,
    bool IsActive,
    DateTimeOffset CreatedAt);

public sealed record CreateWorkerRequest(
    string Username,
    string Password,
    string Name,
    string Surname,
    string PhoneNumber,
    WorkerRole Role);

public sealed record UpdateWorkerRequest(string Name, string Surname, string PhoneNumber, WorkerRole Role);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record WorkerListQuery(
    string? Search = null,
    WorkerRole? Role = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = Common.Paging.DefaultPageSize);
