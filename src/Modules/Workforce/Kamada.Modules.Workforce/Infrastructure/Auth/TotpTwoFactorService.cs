using System.Security.Cryptography;
using System.Text;
using Kamada.Modules.Workforce.Application.Abstractions;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace Kamada.Modules.Workforce.Infrastructure.Auth;

internal sealed class TotpTwoFactorService(IDataProtectionProvider dataProtection, TimeProvider clock) : ITwoFactorService
{
    private const string Issuer = "KAMADA";
    private const int SecretBytes = 20; // 160 bits, as RFC 4226 recommends for HMAC-SHA1

    // Recovery codes avoid look-alike characters (0/o, 1/l/i) so they can be typed from paper.
    private const string RecoveryAlphabet = "abcdefghjkmnpqrstuvwxyz23456789";

    private readonly IDataProtector _secrets = dataProtection.CreateProtector("Kamada.Workforce.TwoFactor.Secret");
    private readonly ITimeLimitedDataProtector _challenges =
        dataProtection.CreateProtector("Kamada.Workforce.TwoFactor.Challenge").ToTimeLimitedDataProtector();

    public TimeSpan ChallengeLifetime { get; } = TimeSpan.FromMinutes(5);

    public TwoFactorKey CreateKey()
    {
        var secret = KeyGeneration.GenerateRandomKey(SecretBytes);
        var sharedKey = Base32Encoding.ToString(secret).TrimEnd('=');
        return new TwoFactorKey(sharedKey, _secrets.Protect(sharedKey));
    }

    public string AuthenticatorUri(string account, string sharedKey) =>
        $"otpauth://totp/{Uri.EscapeDataString(Issuer)}:{Uri.EscapeDataString(account)}" +
        $"?secret={sharedKey}&issuer={Uri.EscapeDataString(Issuer)}&digits=6&period=30";

    public string FormatSharedKey(string sharedKey) =>
        string.Join(' ', sharedKey.ToLowerInvariant().Chunk(4).Select(chunk => new string(chunk)));

    public long? VerifyCode(string protectedSecret, string code, DateTimeOffset now)
    {
        var digits = new string([.. code.Where(char.IsAsciiDigit)]);
        if (digits.Length != 6)
        {
            return null;
        }

        var totp = new Totp(Base32Encoding.ToBytes(_secrets.Unprotect(protectedSecret)));
        return totp.VerifyTotp(now.UtcDateTime, digits, out var step, VerificationWindow.RfcSpecifiedNetworkDelay) ? step : null;
    }

    public string IssueChallenge(Guid workerId) =>
        _challenges.Protect(workerId.ToString("N"), clock.GetUtcNow() + ChallengeLifetime);

    public Guid? ReadChallenge(string challenge)
    {
        try
        {
            // The time-limited protector checks expiry against the system clock.
            return Guid.TryParseExact(_challenges.Unprotect(challenge), "N", out var workerId) ? workerId : null;
        }
        catch (CryptographicException)
        {
            return null; // tampered, expired or from another key ring
        }
    }

    public IReadOnlyList<string> CreateRecoveryCodes(int count) =>
        [.. Enumerable.Range(0, count).Select(_ => $"{RandomPart()}-{RandomPart()}")];

    public string HashRecoveryCode(string code)
    {
        var normalized = new string([.. code.Where(char.IsAsciiLetterOrDigit).Select(char.ToLowerInvariant)]);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(normalized)));
    }

    private static string RandomPart() => RandomNumberGenerator.GetString(RecoveryAlphabet, 5);
}
