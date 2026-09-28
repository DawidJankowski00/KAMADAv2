using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Workers;

/// <summary>
/// Employee who can sign in: an administrator (back office) or a courier (mobile app).
/// </summary>
internal sealed class Worker : Entity, IAuditable
{
    private Worker(Guid id, string username, string passwordHash, string name, string surname, string phoneNumber, WorkerRole role)
        : base(id)
    {
        Username = username;
        PasswordHash = passwordHash;
        Name = name;
        Surname = surname;
        PhoneNumber = phoneNumber;
        Role = role;
        IsActive = true;
    }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public string Name { get; private set; }

    public string Surname { get; private set; }

    public string PhoneNumber { get; private set; }

    public WorkerRole Role { get; private set; }

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string FullName => $"{Name} {Surname}";

    /// <summary>Usernames are case-insensitive; they are stored normalized.</summary>
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static Worker Create(string username, string passwordHash, string name, string surname, string phoneNumber, WorkerRole role) =>
        new(Guid.CreateVersion7(), NormalizeUsername(username), passwordHash, name.Trim(), surname.Trim(), phoneNumber.Trim(), role);

    public void UpdateProfile(string name, string surname, string phoneNumber)
    {
        Name = name.Trim();
        Surname = surname.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    public Result ChangeRole(WorkerRole role, Guid changedBy)
    {
        if (role == Role)
        {
            return Result.Success();
        }

        if (changedBy == Id)
        {
            return WorkerErrors.CannotChangeOwnRole;
        }

        Role = role;
        return Result.Success();
    }

    public void ChangePasswordHash(string passwordHash) => PasswordHash = passwordHash;

    public Result Deactivate(Guid deactivatedBy)
    {
        if (deactivatedBy == Id)
        {
            return WorkerErrors.CannotDeactivateSelf;
        }

        IsActive = false;
        return Result.Success();
    }

    public void Activate() => IsActive = true;
}
