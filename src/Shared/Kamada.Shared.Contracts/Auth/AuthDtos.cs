namespace Kamada.Shared.Contracts.Auth;

public sealed record LoginRequest(string Username, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>
/// Answer to a sign-in with username and password: either the session (<see cref="Tokens"/>) or, for an account with
/// two-factor authentication, a challenge to finish with a code (<see cref="TwoFactor"/>). Exactly one is set.
/// </summary>
public sealed record LoginResponse(TokenResponse? Tokens, TwoFactorChallengeDto? TwoFactor);

/// <param name="ChallengeToken">Sent back with the code to <c>POST /api/auth/login/two-factor</c>; valid a few minutes.</param>
public sealed record TwoFactorChallengeDto(string ChallengeToken, DateTimeOffset ExpiresAt);

/// <param name="Code">6 digits from the authenticator app, or a recovery code (<c>xxxxx-xxxxx</c>).</param>
public sealed record TwoFactorLoginRequest(string ChallengeToken, string Code);

public sealed record TwoFactorStatusDto(bool Enabled, int RecoveryCodesLeft);

/// <param name="SharedKey">For typing into the app by hand (groups of four).</param>
/// <param name="AuthenticatorUri">The <c>otpauth://</c> URI the QR code encodes.</param>
public sealed record TwoFactorSetupDto(string SharedKey, string AuthenticatorUri);

public sealed record TwoFactorCodeRequest(string Code);

/// <summary>Shown once; each code signs in once instead of an authenticator code.</summary>
public sealed record RecoveryCodesDto(IReadOnlyList<string> Codes);

/// <param name="Code">An authenticator or recovery code, so a stolen password alone cannot switch 2FA off.</param>
public sealed record DisableTwoFactorRequest(string Password, string Code);

public sealed record ForgotPasswordRequest(string Email);

/// <summary>One signed-in device of the worker (a refresh token family).</summary>
/// <param name="LastUsedAt">When the session last refreshed its access token (every ≤ 15 min while the app is open).</param>
/// <param name="IsCurrent">The session of the request that listed the sessions.</param>
public sealed record SessionDto(
    Guid Id,
    DateTimeOffset StartedAt,
    DateTimeOffset LastUsedAt,
    string? UserAgent,
    string? IpAddress,
    bool IsCurrent);

/// <param name="Token">The token from the reset link in the e-mail.</param>
public sealed record CompletePasswordResetRequest(string Token, string NewPassword);

/// <param name="Roles">Every role of the worker (role names from <see cref="Kamada.Shared.Contracts.Auth.Roles"/>).</param>
/// <param name="TwoFactorSetupRequired">
/// An administrator without 2FA where it is required: the API answers only the account endpoints until it is set up.
/// </param>
public sealed record AuthenticatedUserDto(
    Guid Id,
    string Username,
    string Name,
    string Surname,
    IReadOnlyList<string> Roles,
    bool TwoFactorSetupRequired = false);

public sealed record TokenResponse(
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt,
    AuthenticatedUserDto User);
