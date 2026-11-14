using Kamada.Shared.Kernel.Results;

namespace Kamada.Modules.Workforce.Domain.Auth;

internal static class AuthErrors
{
    /// <summary>Deliberately vague: does not reveal whether the user exists or is deactivated.</summary>
    public static readonly Error InvalidCredentials =
        Error.Unauthorized("Auth.InvalidCredentials", "Invalid username or password.");

    /// <summary>400, not 401: the session is valid, and a 401 would make the apps refresh or sign out.</summary>
    public static readonly Error CurrentPasswordInvalid =
        Error.Validation("Auth.CurrentPasswordInvalid", "The current password is not correct.");

    /// <summary>One answer for unknown, expired and spent links.</summary>
    public static readonly Error InvalidResetToken =
        Error.Validation("Auth.InvalidResetToken", "The password reset link is invalid or has expired. Ask for a new one.");

    public static readonly Error InvalidRefreshToken =
        Error.Unauthorized("Auth.InvalidRefreshToken", "The refresh token is invalid or expired. Sign in again.");
}
