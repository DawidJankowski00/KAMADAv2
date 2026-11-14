using System.Net;
using Kamada.IntegrationTests.Infrastructure;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Workforce;
using OtpNet;
using WorkerRole = Kamada.Modules.Workforce.Domain.Workers.WorkerRole;

namespace Kamada.IntegrationTests.Workforce;

public sealed class TwoFactorTests(KamadaApiFactory factory)
{
    private readonly HttpClient _anonymous = factory.CreateClient();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Enable_ThenLogin_NeedsTheCodeAfterThePassword()
    {
        var (worker, totp, _) = await WorkerWithTwoFactorAsync();

        var login = await (await _anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password)))
            .ReadAsAsync<LoginResponse>();

        login.Tokens.ShouldBeNull();
        login.TwoFactor.ShouldNotBeNull();
        var finish = await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(login.TwoFactor.ChallengeToken, NextCode(totp)));
        finish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await finish.ReadAsAsync<TokenResponse>()).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task Login_SameCodeTwice_SecondTimeIsRejectedAsAReplay()
    {
        var (worker, totp, _) = await WorkerWithTwoFactorAsync();
        var code = NextCode(totp);
        (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), code)))
            .StatusCode.ShouldBe(HttpStatusCode.OK);

        var again = await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), code));

        again.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await again.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidTwoFactorLogin");
    }

    [Fact]
    public async Task Login_RecoveryCode_WorksOnce()
    {
        var (worker, _, recoveryCodes) = await WorkerWithTwoFactorAsync();

        (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), recoveryCodes[0].ToUpperInvariant())))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
        (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), recoveryCodes[0])))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WrongCodes_LockTheAccountEvenWhenThePasswordIsEnteredAgain()
    {
        var (worker, totp, _) = await WorkerWithTwoFactorAsync();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            // A fresh challenge each time: the right password must not reset the count of wrong codes.
            (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), "000000")))
                .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        var login = await _anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password));

        login.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await login.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_ForgedChallenge_Returns401()
    {
        var response = await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest("forged", "123456"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await response.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidTwoFactorLogin");
    }

    [Fact]
    public async Task Enable_WithWrongCode_Returns400AndWithoutSetup_Returns409()
    {
        var (client, _) = await factory.CreateSignedInClientAsync(WorkerRole.Courier);

        var withoutSetup = await client.PostJsonAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest("123456"));
        await client.PostAsync("/api/auth/two-factor/setup", null, Ct);
        var wrongCode = await client.PostJsonAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest("000000"));

        withoutSetup.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await withoutSetup.ReadProblemCodeAsync()).ShouldBe("Workers.TwoFactorNotSetUp");
        wrongCode.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await wrongCode.ReadProblemCodeAsync()).ShouldBe("Auth.InvalidTwoFactorCode");
        (await (await client.GetAsync("/api/auth/two-factor", Ct)).ReadAsAsync<TwoFactorStatusDto>()).Enabled.ShouldBeFalse();
    }

    [Fact]
    public async Task Disable_NeedsPasswordAndCode()
    {
        var (worker, totp, recoveryCodes) = await WorkerWithTwoFactorAsync();
        var client = await SignedInAsync(worker, totp);

        var wrongPassword = await client.PostJsonAsync("/api/auth/two-factor/disable", new DisableTwoFactorRequest("Wrong1234!", recoveryCodes[0]));
        var disabled = await client.PostJsonAsync("/api/auth/two-factor/disable", new DisableTwoFactorRequest(worker.Password, recoveryCodes[0]));

        wrongPassword.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        disabled.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await _anonymous.LoginAsync(worker.Username, worker.Password)).User.Id.ShouldBe(worker.Id);
    }

    [Fact]
    public async Task RecoveryCodes_Regenerated_OldOnesStopWorking()
    {
        var (worker, totp, oldCodes) = await WorkerWithTwoFactorAsync();
        var client = await SignedInAsync(worker, totp);

        var response = await client.PostJsonAsync("/api/auth/two-factor/recovery-codes", new TwoFactorCodeRequest(oldCodes[0]));

        var newCodes = (await response.ReadAsAsync<RecoveryCodesDto>()).Codes;
        newCodes.Count.ShouldBe(10);
        (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), oldCodes[1])))
            .StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), newCodes[0])))
            .StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResetByAdmin_LetsTheWorkerSignInWithThePasswordOnly()
    {
        var (admin, _) = await factory.CreateSignedInClientAsync(WorkerRole.Admin);
        var (worker, _, _) = await WorkerWithTwoFactorAsync();
        (await (await admin.GetAsync($"/api/workers/{worker.Id}", Ct)).ReadAsAsync<WorkerDto>()).TwoFactorEnabled.ShouldBeTrue();

        (await admin.PostAsync($"/api/workers/{worker.Id}/reset-two-factor", null, Ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await _anonymous.LoginAsync(worker.Username, worker.Password)).User.Id.ShouldBe(worker.Id);
    }

    /// <summary>A worker who went through setup and enable; the code used for enabling belongs to the current step.</summary>
    private async Task<(TestWorker Worker, Totp Totp, IReadOnlyList<string> RecoveryCodes)> WorkerWithTwoFactorAsync()
    {
        var worker = await factory.CreateWorkerAsync(WorkerRole.Admin);
        var client = factory.CreateClient().WithBearer((await _anonymous.LoginAsync(worker.Username, worker.Password)).AccessToken);

        var setup = await (await client.PostAsync("/api/auth/two-factor/setup", null, Ct)).ReadAsAsync<TwoFactorSetupDto>();
        setup.AuthenticatorUri.ShouldStartWith("otpauth://totp/KAMADA:");
        var totp = new Totp(Base32Encoding.ToBytes(setup.SharedKey.Replace(" ", string.Empty, StringComparison.Ordinal).ToUpperInvariant()));
        var enable = await client.PostJsonAsync("/api/auth/two-factor/enable", new TwoFactorCodeRequest(totp.ComputeTotp(DateTime.UtcNow)));
        enable.StatusCode.ShouldBe(HttpStatusCode.OK);
        var codes = (await enable.ReadAsAsync<RecoveryCodesDto>()).Codes;
        codes.Count.ShouldBe(10);
        return (worker, totp, codes);
    }

    private async Task<string> ChallengeAsync(TestWorker worker) =>
        (await (await _anonymous.PostJsonAsync("/api/auth/login", new LoginRequest(worker.Username, worker.Password)))
            .ReadAsAsync<LoginResponse>()).TwoFactor!.ChallengeToken;

    private async Task<HttpClient> SignedInAsync(TestWorker worker, Totp totp)
    {
        var tokens = await (await _anonymous.PostJsonAsync("/api/auth/login/two-factor", new TwoFactorLoginRequest(await ChallengeAsync(worker), NextCode(totp))))
            .ReadAsAsync<TokenResponse>();
        return factory.CreateClient().WithBearer(tokens.AccessToken);
    }

    /// <summary>
    /// The code of the next time step: the server accepts one step of clock drift, and the code used just before (for
    /// enabling) belongs to the current step, so this one is not a replay. Tests needing a third code use a recovery code.
    /// </summary>
    private static string NextCode(Totp totp) => totp.ComputeTotp(DateTime.UtcNow.AddSeconds(30));
}
