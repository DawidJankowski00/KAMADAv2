using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Workforce.Domain.Auth;

/// <summary>
/// One-time link to set a new password, sent by e-mail. Only a hash of the token is stored; it expires quickly and is
/// spent on first use. A newer request revokes the older links of the same worker.
/// </summary>
internal sealed class PasswordResetToken : Entity, IAuditable
{
    private PasswordResetToken(Guid id, Guid workerId, string tokenHash, DateTimeOffset expiresAt)
        : base(id)
    {
        WorkerId = workerId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid WorkerId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    /// <summary>When the token was spent or revoked; a token is usable only while this is empty.</summary>
    public DateTimeOffset? UsedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public static PasswordResetToken Issue(Guid workerId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new(Guid.CreateVersion7(), workerId, tokenHash, now + lifetime);

    public bool IsUsable(DateTimeOffset now) => UsedAt is null && now < ExpiresAt;

    /// <summary>Spends or revokes the token (idempotent).</summary>
    public void Invalidate(DateTimeOffset now) => UsedAt ??= now;
}
