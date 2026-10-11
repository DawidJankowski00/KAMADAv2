using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Workers;

/// <summary>
/// Employee who can sign in. A worker has one or more roles (e.g. an administrator who also delivers).
/// </summary>
internal sealed class Worker : Entity, IAuditable
{
    private List<WorkerRole> _roles = [];

    private Worker(Guid id, string username, string passwordHash, string name, string surname, string phoneNumber)
        : base(id)
    {
        Username = username;
        PasswordHash = passwordHash;
        Name = name;
        Surname = surname;
        PhoneNumber = phoneNumber;
        IsActive = true;
    }

    public string Username { get; private set; }

    public string PasswordHash { get; private set; }

    public string Name { get; private set; }

    public string Surname { get; private set; }

    public string PhoneNumber { get; private set; }

    /// <summary>Distinct roles in enum order, never empty.</summary>
    public IReadOnlyList<WorkerRole> Roles => _roles;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string FullName => $"{Name} {Surname}";

    /// <summary>Usernames are case-insensitive; they are stored normalized.</summary>
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static Result<Worker> Create(
        string username,
        string passwordHash,
        string name,
        string surname,
        string phoneNumber,
        IReadOnlyCollection<WorkerRole> roles)
    {
        var validation = ValidateRoles(roles);
        if (validation.IsFailure)
        {
            return validation.Error;
        }

        var worker = new Worker(Guid.CreateVersion7(), NormalizeUsername(username), passwordHash, name.Trim(), surname.Trim(), phoneNumber.Trim())
        {
            _roles = [.. roles.Order()],
        };
        return worker;
    }

    public bool HasRole(WorkerRole role) => _roles.Contains(role);

    public void UpdateProfile(string name, string surname, string phoneNumber)
    {
        Name = name.Trim();
        Surname = surname.Trim();
        PhoneNumber = phoneNumber.Trim();
    }

    /// <summary>
    /// Replaces the roles. Nobody can take the Admin role away from themselves, so there is always an administrator
    /// left who can undo a change.
    /// </summary>
    public Result SetRoles(IReadOnlyCollection<WorkerRole> roles, Guid changedBy)
    {
        var validation = ValidateRoles(roles);
        if (validation.IsFailure)
        {
            return validation;
        }

        if (changedBy == Id && HasRole(WorkerRole.Admin) && !roles.Contains(WorkerRole.Admin))
        {
            return WorkerErrors.CannotRemoveOwnAdminRole;
        }

        if (!_roles.SequenceEqual(roles.Order()))
        {
            _roles = [.. roles.Order()];
        }

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

    private static Result ValidateRoles(IReadOnlyCollection<WorkerRole> roles)
    {
        if (roles.Count == 0)
        {
            return WorkerErrors.RolesRequired;
        }

        return roles.Distinct().Count() == roles.Count ? Result.Success() : WorkerErrors.DuplicateRoles;
    }
}
