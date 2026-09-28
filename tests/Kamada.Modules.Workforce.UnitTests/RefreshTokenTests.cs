using Kamada.Modules.Workforce.Domain.Auth;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class RefreshTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    [Fact]
    public void Issue_StartsNewFamily()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, Lifetime);

        token.FamilyId.ShouldBe(token.Id);
        token.ExpiresAt.ShouldBe(Now + Lifetime);
        token.IsActive(Now).ShouldBeTrue();
    }

    [Fact]
    public void IsActive_AfterExpiry_ReturnsFalse()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, Lifetime);

        token.IsActive(Now + Lifetime).ShouldBeFalse();
    }

    [Fact]
    public void Rotate_RevokesCurrentAndReturnsSuccessorInSameFamily()
    {
        var workerId = Guid.NewGuid();
        var token = RefreshToken.Issue(workerId, "hash-1", Now, Lifetime);
        var later = Now.AddHours(1);

        var successor = token.Rotate("hash-2", later, Lifetime);

        token.IsRevoked.ShouldBeTrue();
        token.ReplacedByTokenId.ShouldBe(successor.Id);
        successor.FamilyId.ShouldBe(token.FamilyId);
        successor.WorkerId.ShouldBe(workerId);
        successor.TokenHash.ShouldBe("hash-2");
        successor.ExpiresAt.ShouldBe(later + Lifetime);
        successor.IsActive(later).ShouldBeTrue();
    }

    [Fact]
    public void Rotate_RevokedToken_Throws()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, Lifetime);
        token.Revoke(Now);

        Should.Throw<InvalidOperationException>(() => token.Rotate("next", Now, Lifetime));
    }

    [Fact]
    public void Revoke_Twice_KeepsFirstRevocationTime()
    {
        var token = RefreshToken.Issue(Guid.NewGuid(), "hash", Now, Lifetime);

        token.Revoke(Now);
        token.Revoke(Now.AddDays(1));

        token.RevokedAt.ShouldBe(Now);
    }
}
