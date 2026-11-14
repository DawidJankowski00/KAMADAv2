namespace Kamada.Modules.Workforce.Application.Abstractions;

/// <summary>A new TOTP secret: <see cref="SharedKey"/> goes to the worker once, <see cref="ProtectedSecret"/> is stored.</summary>
internal sealed record TwoFactorKey(string SharedKey, string ProtectedSecret);

/// <summary>
/// TOTP (RFC 6238: SHA-1, 6 digits, 30-second steps, ±1 step of clock drift), encryption of the secrets and the
/// short-lived token that links the two steps of a 2FA sign-in.
/// </summary>
internal interface ITwoFactorService
{
    TwoFactorKey CreateKey();

    /// <summary><c>otpauth://</c> URI for the QR code an authenticator app scans.</summary>
    string AuthenticatorUri(string account, string sharedKey);

    /// <summary>Shared key in a form that is easy to type: lower case, groups of four.</summary>
    string FormatSharedKey(string sharedKey);

    /// <summary>The time step the code belongs to, or null when the code does not match.</summary>
    long? VerifyCode(string protectedSecret, string code, DateTimeOffset now);

    /// <summary>Opaque token proving that the password of <paramref name="workerId"/> was right (valid 5 minutes).</summary>
    string IssueChallenge(Guid workerId);

    TimeSpan ChallengeLifetime { get; }

    /// <summary>The worker of a valid, unexpired challenge; null otherwise.</summary>
    Guid? ReadChallenge(string challenge);

    /// <summary>Random recovery codes in the form <c>xxxxx-xxxxx</c>.</summary>
    IReadOnlyList<string> CreateRecoveryCodes(int count);

    /// <summary>Hash stored for a recovery code (case, spaces and dashes do not matter).</summary>
    string HashRecoveryCode(string code);
}
