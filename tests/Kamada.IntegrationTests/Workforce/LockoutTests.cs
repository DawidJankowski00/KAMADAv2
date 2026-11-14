using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Workforce;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

/// <summary>Default policy (Auth:Lockout): 5 wrong passwords lock the account for 15 minutes.</summary>
public sealed class LockoutTests(KamadaApiFactory factory)
{
    private const int MaxFailedAttempts = 5;

    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Login_AfterTooManyWrongPasswords_RejectsTheRightOneWithTheSameAnswer()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        await FailLoginsAsync(worker, MaxFailedAttempts);

        var response = await _client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_SuccessBeforeTheLimit_StartsCountingAgain()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        await FailLoginsAsync(worker, MaxFailedAttempts - 1);
        await _client.LoginAsync(worker.Username, worker.Password);

        await FailLoginsAsync(worker, MaxFailedAttempts - 1);

        (await _client.LoginAsync(worker.Username, worker.Password)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task Unlock_ByAdmin_LetsTheWorkerSignInAndShowsInTheList()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var worker = await factory.CreateWorkerAsync(WorkerRole.Seller);
        await FailLoginsAsync(worker, MaxFailedAttempts);
        var locked = await (await admin.GetAsync($"/api/workers/{worker.Id}", TestContext.Current.CancellationToken)).ReadAsAsync<WorkerDto>();
        locked.LockedUntil.ShouldNotBeNull();

        var unlock = await admin.PostAsync($"/api/workers/{worker.Id}/unlock", null, TestContext.Current.CancellationToken);

        unlock.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _client.LoginAsync(worker.Username, worker.Password)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task ResetPasswordByEmail_LiftsTheLockout()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Courier);
        await FailLoginsAsync(worker, MaxFailedAttempts);
        await _client.PostJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(worker.Email));
        var text = (await factory.Emails.WaitForAsync(worker.Email)).TextBody;
        var token = Uri.UnescapeDataString(text[(text.IndexOf("?token=", StringComparison.Ordinal) + 7)..].Split('\n')[0].Trim());

        (await _client.PostJsonAsync("/api/auth/reset-password", new CompletePasswordResetRequest(token, "Nowe1234!")))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _client.LoginAsync(worker.Username, "Nowe1234!")).User.Id.ShouldBe(worker.Id);
    }

    private async Task FailLoginsAsync(TestWorker worker, int count)
    {
        for (var attempt = 0; attempt < count; attempt++)
        {
            (await _client.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, "Wrong1234!")))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }
}
