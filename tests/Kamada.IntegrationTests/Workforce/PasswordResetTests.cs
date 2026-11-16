using System.Net;
using System.Text.RegularExpressions;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed partial class PasswordResetTests(KamadaApiFactory factory)
{
    private const string NewPassword = "Nowe-haslo-do-testow";

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task ForgotPassword_KnownEmail_SendsLinkThatSetsNewPasswordAndEndsSessions()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        var session = await _client.LoginAsync(worker.Username, worker.Password);

        var forgot = await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(worker.Email.ToUpperInvariant()));

        forgot.StatusCode.ShouldBe(HttpStatusCode.Accepted);
        var email = await factory.Emails.WaitForAsync(worker.Email);
        email.Subject.ShouldBe("KAMADA – ustawienie nowego hasła");
        email.TextBody.ShouldContain(KamadaApiFactory.ResetPasswordUrl + "?token=");
        var token = TokenFrom(email.TextBody);

        var reset = await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(token, NewPassword));

        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.PostJsonAsync("/api/auth/refresh", new RefreshTokenRequest(session.RefreshToken)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password)))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _client.LoginAsync(worker.Username, NewPassword)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task ResetPassword_TokenUsedTwice_SecondTimeReturns400()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Seller);
        await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(worker.Email));
        var token = TokenFrom((await factory.Emails.WaitForAsync(worker.Email)).TextBody);
        (await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(token, NewPassword))).EnsureSuccessStatusCode();

        var again = await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(token, "Inne-haslo-do-testow"));

        again.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await again.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidResetToken");
    }

    [Fact]
    public async Task ForgotPassword_SecondRequest_RevokesTheFirstLink()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(worker.Email));
        var first = TokenFrom((await factory.Emails.WaitForAsync(worker.Email)).TextBody);
        await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(worker.Email));
        var second = TokenFrom((await factory.Emails.WaitForAsync(worker.Email, count: 2)).TextBody);

        (await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(first, NewPassword)))
            .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(second, NewPassword)))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ForgotPassword_UnknownOrInactiveAccount_Returns202AndSendsNothing()
    {
        var inactive = await factory.CreateWorkerAsync(WorkerRole.Courier, isActive: false);
        var unknown = $"{Guid.NewGuid():N}@kamada.test";

        (await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(unknown)))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);
        (await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(inactive.Email)))
            .StatusCode.ShouldBe(HttpStatusCode.Accepted);

        await Task.Delay(300, TestContext.Current.CancellationToken);
        factory.Emails.SentTo(unknown).ShouldBeEmpty();
        factory.Emails.SentTo(inactive.Email).ShouldBeEmpty();
    }

    [Fact]
    public async Task ResetPassword_UnknownToken_Returns400()
    {
        var response = await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest("not-a-real-token", NewPassword));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidResetToken");
    }

    private static string TokenFrom(string text) => Uri.UnescapeDataString(TokenPattern().Match(text).Groups[1].Value);

    [GeneratedRegex(@"\?token=(\S+)")]
    private static partial Regex TokenPattern();
}
