namespace Kamada.Shared.Contracts.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

public sealed record ForgotPasswordRequest(string Email);

/// <param name="Token">The token from the reset link in the e-mail.</param>
public sealed record CompletePasswordResetRequest(string Token, string NewPassword);

/// <param name="Roles">Every role of the worker (role names from <see cref="Kamada.Shared.Contracts.Auth.Roles"/>).</param>
public sealed record AuthenticatedUserDto(Guid Id, string Username, string Name, string Surname, IReadOnlyList<string> Roles);

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUserDto User);
