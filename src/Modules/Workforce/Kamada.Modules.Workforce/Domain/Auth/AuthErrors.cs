using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Auth;

internal static class AuthErrors
{
    /// <summary>Deliberately vague: does not reveal whether the user exists or is deactivated.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired. Sign in again.");
}
