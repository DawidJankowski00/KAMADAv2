using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Auth;
using Kamada.Shared.Infrastructure.Auth;
using Microsoft.Extensions.Time.Testing;
using Microsoft.IdentityModel.JsonWebTokens;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class JwtTokenServiceTests
{
    private static readonly JwtOptions Options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "unit-test-signing-key-0123456789-abcdef",
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(7),
    };

    private readonly FakeTimeProvider _clock = new(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

    private JwtTokenService CreateService() => new(MsOptions.Create(Options), _clock);

    [Fact]
    public void CreateAccessToken_ContainsIdentityAndRoleClaims()
    {
        var worker = Worker.Create("admin", "hash", "Anna", "Nowak", "+48 600 000 000", [WorkerRole.Admin]).Value;

        var accessToken = CreateService().CreateAccessToken(worker);

        accessToken.ExpiresAt.ShouldBe(_clock.GetUtcNow() + Options.AccessTokenLifetime);
        var jwt = new JsonWebToken(accessToken.Token);
        jwt.Issuer.ShouldBe(Options.Issuer);
        jwt.Audiences.ShouldBe([Options.Audience]);
        jwt.Subject.ShouldBe(worker.Id.ToString());
        jwt.GetClaim(KamadaClaimTypes.Role).Value.ShouldBe("Admin");
        jwt.GetClaim(KamadaClaimTypes.Name).Value.ShouldBe("admin");
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsUniqueTokenWithMatchingHash()
    {
        var service = CreateService();

        var first = service.GenerateRefreshToken();
        var second = service.GenerateRefreshToken();

        first.Token.ShouldNotBe(second.Token);
        service.HashRefreshToken(first.Token).ShouldBe(first.Hash);
        first.Hash.ShouldNotBe(first.Token);
        first.Hash.Length.ShouldBe(64);
    }
}
