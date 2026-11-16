using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Domain.Auth;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class PasswordResetTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsUsable_BeforeExpiry_True_AfterExpiry_False()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromMinutes(30));

        token.IsUsable(Now.AddMinutes(29)).ShouldBeTrue();
        token.IsUsable(Now.AddMinutes(30)).ShouldBeFalse();
    }

    [Fact]
    public void Invalidate_SpendsTheTokenAndKeepsTheFirstMoment()
    {
        var token = PasswordResetToken.Issue(Guid.NewGuid(), "hash", Now, TimeSpan.FromMinutes(30));

        token.Invalidate(Now.AddMinutes(1));
        token.Invalidate(Now.AddMinutes(2));

        token.IsUsable(Now.AddMinutes(3)).ShouldBeFalse();
        token.UsedAt.ShouldBe(Now.AddMinutes(1));
    }

    [Fact]
    public void ResetLink_AddsTheEscapedTokenToThePage()
    {
        var link = ForgotPasswordHandler.ResetLink(new Uri("https://panel.example/reset-password"), "a+b/c=");

        link.AbsoluteUri.ShouldBe("https://panel.example/reset-password?token=a%2Bb%2Fc%3D");
    }
}
