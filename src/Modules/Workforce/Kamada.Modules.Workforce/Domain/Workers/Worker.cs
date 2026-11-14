using Kamada.Shared.Kernel.Domain;
using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Workers;

/// <summary>
/// Employee who can sign in. A worker has one or more roles (e.g. an administrator who also delivers).
/// </summary>
internal sealed class Worker : Entity, IAuditable
{
    private List<WorkerRole> _roles = [];

    private Worker(Guid id, string username, string email, string passwordHash, string name, string surname, string phoneNumber)
        : base(id)
    {
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Name = name;
        Surname = surname;
        PhoneNumber = phoneNumber;
        IsActive = true;
    }

    public string Username { get; private set; }

    /// <summary>Unique, stored normalized (trimmed, lower case); used for password reset e-mails.</summary>
    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public string Name { get; private set; }

    public string Surname { get; private set; }

    public string PhoneNumber { get; private set; }

    /// <summary>Distinct roles in enum order, never empty.</summary>
    public IReadOnlyList<WorkerRole> Roles => _roles;

    public bool IsActive { get; private set; }

    /// <summary>Wrong passwords since the last successful login or lockout.</summary>
    public int FailedLoginCount { get; private set; }

    /// <summary>Sign-in is refused until then (after too many wrong passwords).</summary>
    public DateTimeOffset? LockedUntil { get; private set; }

    /// <summary>Sign-in also needs a code from an authenticator app (TOTP).</summary>
    public bool TwoFactorEnabled { get; private set; }

    /// <summary>The TOTP secret, encrypted with Data Protection; never stored or logged in plain text.</summary>
    public string? TwoFactorSecret { get; private set; }

    /// <summary>A secret being set up: becomes <see cref="TwoFactorSecret"/> once the worker confirms a code from it.</summary>
    public string? PendingTwoFactorSecret { get; private set; }

    /// <summary>Time step of the last accepted code; a code from the same or an earlier step is a replay.</summary>
    public long? TwoFactorLastStep { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public string FullName => $"{Name} {Surname}";

    /// <summary>Usernames are case-insensitive; they are stored normalized.</summary>
    public static string NormalizeUsername(string username) => username.Trim().ToLowerInvariant();

    public static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public static Result<Worker> Create(
        string username,
        string email,
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

        var worker = new Worker(Guid.CreateVersion7(), NormalizeUsername(username), NormalizeEmail(email), passwordHash, name.Trim(), surname.Trim(), phoneNumber.Trim())
        {
            _roles = [.. roles.Order()],
        };
        return worker;
    }

    public bool HasRole(WorkerRole role) => _roles.Contains(role);

    public void UpdateProfile(string email, string name, string surname, string phoneNumber)
    {
        Email = NormalizeEmail(email);
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

    public bool IsLockedOut(DateTimeOffset now) => LockedUntil > now;

    /// <summary>
    /// Counts a wrong password; the <paramref name="maxAttempts"/>-th one locks the account for
    /// <paramref name="lockout"/> and starts counting again. Attempts while locked are not counted.
    /// </summary>
    public void RegisterFailedLogin(DateTimeOffset now, int maxAttempts, TimeSpan lockout)
    {
        if (IsLockedOut(now))
        {
            return;
        }

        FailedLoginCount++;
        if (FailedLoginCount >= maxAttempts)
        {
            LockedUntil = now + lockout;
            FailedLoginCount = 0;
        }
    }

    /// <summary>Starts (or restarts) setting up 2FA with a new encrypted secret; 2FA stays off until confirmed.</summary>
    public Result BeginTwoFactorSetup(string protectedSecret)
    {
        if (TwoFactorEnabled)
        {
            return WorkerErrors.TwoFactorAlreadyEnabled;
        }

        PendingTwoFactorSecret = protectedSecret;
        return Result.Success();
    }

    /// <summary>Turns 2FA on with the pending secret, after the worker proved it with a code from step <paramref name="step"/>.</summary>
    public Result EnableTwoFactor(long step)
    {
        if (TwoFactorEnabled)
        {
            return WorkerErrors.TwoFactorAlreadyEnabled;
        }

        if (PendingTwoFactorSecret is null)
        {
            return WorkerErrors.TwoFactorNotSetUp;
        }

        TwoFactorSecret = PendingTwoFactorSecret;
        PendingTwoFactorSecret = null;
        TwoFactorEnabled = true;
        TwoFactorLastStep = step;
        return Result.Success();
    }

    public void DisableTwoFactor()
    {
        TwoFactorEnabled = false;
        TwoFactorSecret = null;
        PendingTwoFactorSecret = null;
        TwoFactorLastStep = null;
    }

    /// <summary>
    /// Accepts a code's time step once: false for the step of an already accepted code or an earlier one (a replay of a
    /// code seen by someone else, e.g. over the worker's shoulder).
    /// </summary>
    public bool AcceptTwoFactorStep(long step)
    {
        if (step <= TwoFactorLastStep)
        {
            return false;
        }

        TwoFactorLastStep = step;
        return true;
    }

    /// <summary>After a successful login, an admin unlock or a password reset: no lockout, counting from zero.</summary>
    public void ClearFailedLogins()
    {
        FailedLoginCount = 0;
        LockedUntil = null;
    }

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
