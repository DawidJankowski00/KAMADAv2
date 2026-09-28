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

    public static readonly Error CannotChangeOwnRole =
        Error.Conflict("Workers.CannotChangeOwnRole", "You cannot change your own role.");
}
