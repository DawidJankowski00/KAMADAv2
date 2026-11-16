using Kamada.Modules.Workforce.Domain.Workers;
using Kamada.Modules.Workforce.Infrastructure.Auth;
using Microsoft.AspNetCore.DataProtection;
using OtpNet;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class TwoFactorTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly TotpTwoFactorService _service = new(new EphemeralDataProtectionProvider(), TimeProvider.System);

    private static Worker CreateWorker() =>
        Worker.Create("anna", "anna@kamada.test", "hash", "Anna", "Nowak", "+48 600 000 000", [WorkerRole.Admin]).Value;

    [Fact]
    public void EnableTwoFactor_WithoutSetup_Fails_AfterSetup_MovesThePendingSecret()
    {
        var worker = CreateWorker();
        worker.EnableTwoFactor(step: 1).Error.ShouldBe(WorkerErrors.TwoFactorNotSetUp);

        worker.BeginTwoFactorSetup("protected").IsSuccess.ShouldBeTrue();
        worker.EnableTwoFactor(step: 1).IsSuccess.ShouldBeTrue();

        worker.TwoFactorEnabled.ShouldBeTrue();
        worker.TwoFactorSecret.ShouldBe("protected");
        worker.PendingTwoFactorSecret.ShouldBeNull();
        worker.BeginTwoFactorSetup("other").Error.ShouldBe(WorkerErrors.TwoFactorAlreadyEnabled);
    }

    [Fact]
    public void AcceptTwoFactorStep_SameOrEarlierStep_IsAReplay()
    {
        var worker = CreateWorker();
        worker.BeginTwoFactorSetup("protected");
        worker.EnableTwoFactor(step: 100);

        worker.AcceptTwoFactorStep(100).ShouldBeFalse();
        worker.AcceptTwoFactorStep(99).ShouldBeFalse();
        worker.AcceptTwoFactorStep(101).ShouldBeTrue();
        worker.AcceptTwoFactorStep(101).ShouldBeFalse();
    }

    [Fact]
    public void DisableTwoFactor_ForgetsTheSecret()
    {
        var worker = CreateWorker();
        worker.BeginTwoFactorSetup("protected");
        worker.EnableTwoFactor(step: 1);

        worker.DisableTwoFactor();

        worker.TwoFactorEnabled.ShouldBeFalse();
        worker.TwoFactorSecret.ShouldBeNull();
        worker.TwoFactorLastStep.ShouldBeNull();
    }

    [Fact]
    public void VerifyCode_AcceptsCurrentAndNeighbourStepsOnly()
    {
        var key = _service.CreateKey();
        var totp = new Totp(Base32Encoding.ToBytes(key.SharedKey));

        _service.VerifyCode(key.ProtectedSecret, totp.ComputeTotp(Now.UtcDateTime), Now).ShouldNotBeNull();
        _service.VerifyCode(key.ProtectedSecret, totp.ComputeTotp(Now.UtcDateTime.AddSeconds(-30)), Now).ShouldNotBeNull();
        _service.VerifyCode(key.ProtectedSecret, totp.ComputeTotp(Now.UtcDateTime.AddSeconds(90)), Now).ShouldBeNull();
        _service.VerifyCode(key.ProtectedSecret, "12345", Now).ShouldBeNull();
    }

    [Fact]
    public void VerifyCode_IgnoresSpacesTypedInTheMiddle()
    {
        var key = _service.CreateKey();
        var code = new Totp(Base32Encoding.ToBytes(key.SharedKey)).ComputeTotp(Now.UtcDateTime);

        _service.VerifyCode(key.ProtectedSecret, $"{code[..3]} {code[3..]}", Now).ShouldNotBeNull();
    }

    [Fact]
    public void CreateKey_StoresOnlyTheProtectedSecret_AndFormatsTheSharedKeyInGroups()
    {
        var key = _service.CreateKey();

        key.SharedKey.Length.ShouldBe(32); // 160 bits in base32
        key.ProtectedSecret.ShouldNotContain(key.SharedKey);
        _service.FormatSharedKey(key.SharedKey).Split(' ').ShouldAllBe(group => group.Length == 4);
        _service.AuthenticatorUri("anna", key.SharedKey).ShouldBe($"otpauth://totp/KAMADA:anna?secret={key.SharedKey}&issuer=KAMADA&digits=6&period=30");
    }

    [Fact]
    public void Challenge_RoundTrips_AndRejectsGarbage()
    {
        var workerId = Guid.NewGuid();

        _service.ReadChallenge(_service.IssueChallenge(workerId)).ShouldBe(workerId);
        _service.ReadChallenge("garbage").ShouldBeNull();
    }

    [Fact]
    public void RecoveryCodes_AreUniqueAndHashIgnoresCaseAndDashes()
    {
        var codes = _service.CreateRecoveryCodes(10);

        codes.Distinct().Count().ShouldBe(10);
        codes.ShouldAllBe(code => code.Length == 11 && code[5] == '-');
        _service.HashRecoveryCode(codes[0].ToUpperInvariant().Replace("-", " ", StringComparison.Ordinal))
            .ShouldBe(_service.HashRecoveryCode(codes[0]));
    }
}
