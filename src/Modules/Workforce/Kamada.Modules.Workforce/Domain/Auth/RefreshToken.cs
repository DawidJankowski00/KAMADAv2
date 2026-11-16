using Kamada.Shared.Kernel.Domain;

namespace Kamada.Modules.Workforce.Domain.Auth;

/// <summary>
/// Long-lived credential exchanged for a new access token. Only a hash of the token is stored.
/// Tokens are rotated on every use; all tokens descending from one login share a <see cref="FamilyId"/>,
/// so reuse of an already rotated token (a sign of theft) revokes the whole family.
/// </summary>
internal sealed class RefreshToken : Entity, IAuditable
{
    private RefreshToken(Guid id, Guid workerId, Guid familyId, string tokenHash, DateTimeOffset expiresAt)
        : base(id)
    {
        WorkerId = workerId;
        FamilyId = familyId;
        TokenHash = tokenHash;
        ExpiresAt = expiresAt;
    }

    public Guid WorkerId { get; private set; }

    public Guid FamilyId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    /// <summary>User-Agent of the request that issued this token (the device of the session).</summary>
    public string? UserAgent { get; private set; }

    /// <summary>Client IP of the request that issued this token.</summary>
    public string? IpAddress { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public bool IsRevoked => RevokedAt is not null;

    public static RefreshToken Issue(Guid workerId, string tokenHash, DateTimeOffset now, TimeSpan lifetime, SessionClient? client = null)
    {
        var id = Guid.CreateVersion7();
        return new RefreshToken(id, workerId, familyId: id, tokenHash, now + lifetime) { UserAgent = client?.UserAgent, IpAddress = client?.IpAddress };
    }

    public bool IsActive(DateTimeOffset now) => !IsRevoked && now < ExpiresAt;

    /// <summary>Revokes this token and returns its successor in the same family.</summary>
    public RefreshToken Rotate(string newTokenHash, DateTimeOffset now, TimeSpan lifetime, SessionClient? client = null)
    {
        if (!IsActive(now))
        {
            throw new InvalidOperationException("Only an active refresh token can be rotated.");
        }

        var successor = new RefreshToken(Guid.CreateVersion7(), WorkerId, FamilyId, newTokenHash, now + lifetime)
        {
            UserAgent = client?.UserAgent,
            IpAddress = client?.IpAddress,
        };
        RevokedAt = now;
        ReplacedByTokenId = successor.Id;
        return successor;
    }

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;
}
