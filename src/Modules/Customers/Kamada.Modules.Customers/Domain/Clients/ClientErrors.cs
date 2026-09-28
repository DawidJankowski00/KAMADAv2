using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Customers.Domain.Clients;

internal static class ClientErrors
{
    public static readonly Error NotFound =
        Error.NotFound("Clients.NotFound", "Client was not found.");

    public static readonly Error UsernameTaken =
        Error.Conflict("Clients.UsernameTaken", "A client with this username already exists.");

    public static readonly Error EmailTaken =
        Error.Conflict("Clients.EmailTaken", "A client with this e-mail already exists.");

    public static readonly Error AddressNotFound =
        Error.NotFound("Clients.AddressNotFound", "Address was not found.");
}
