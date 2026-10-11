using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Workers;

internal static class WorkerErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Workers.NotFound", "Worker was not found.");

    public static readonly Error UsernameTaken =
        Error.Conflict("Workers.UsernameTaken", "A worker with this username already exists.");

    public static readonly Error CannotDeactivateSelf =
        Error.Conflict("Workers.CannotDeactivateSelf", "You cannot deactivate your own account.");

    public static readonly Error CannotRemoveOwnAdminRole =
        Error.Conflict("Workers.CannotRemoveOwnAdminRole", "You cannot remove the Admin role from your own account.");

    public static readonly Error RolesRequired =
        Error.Validation("Workers.RolesRequired", "A worker must have at least one role.");

    public static readonly Error DuplicateRoles =
        Error.Validation("Workers.DuplicateRoles", "Each role may be given only once.");
}
