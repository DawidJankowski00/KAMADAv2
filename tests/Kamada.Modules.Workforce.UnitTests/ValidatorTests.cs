using Kamada.Modules.Workforce.Application.Auth;
using Kamada.Modules.Workforce.Application.Workers;
using Kamada.Shared.Contracts.Auth;
using Kamada.Shared.Contracts.Workforce;
using Microsoft.Extensions.Options;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class ValidatorTests
{
    private static readonly PasswordPolicy Policy = new(Options.Create(new PasswordPolicyOptions()));

    private static CreateWorkerRequest ValidRequest() =>
        new("kurier9", "kurier9@kamada.test", "Secret-pass-2026", "Jan", "Kowalski", "+48 600 000 000", [WorkerRole.Courier]);

    [Fact]
    public void CreateWorker_ValidRequest_Passes()
    {
        new CreateWorkerRequestValidator(Policy).Validate(ValidRequest()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void CreateWorker_InvalidEmail_Fails(string email)
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Email = email });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Email));
    }

    [Theory]
    [InlineData("Short-pass1")]
    [InlineData("password1234")]
    [InlineData("QWERTY123456")]
    [InlineData("zzzzzzzzzzzzzz")]
    [InlineData("my-kurier9-password")]
    public void CreateWorker_WeakPassword_Fails(string password)
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Password = password });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Password));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("zażółć")]
    public void CreateWorker_InvalidUsername_Fails(string username)
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Username = username });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Username));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("phone-number")]
    public void CreateWorker_InvalidPhone_Fails(string phone)
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { PhoneNumber = phone });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.PhoneNumber));
    }

    [Fact]
    public void CreateWorker_SeveralRoles_Passes()
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Roles = [WorkerRole.Admin, WorkerRole.Courier] });

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateWorker_NoRoles_Fails()
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Roles = [] });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Roles));
    }

    [Fact]
    public void CreateWorker_DuplicateRoles_Fails()
    {
        var result = new CreateWorkerRequestValidator(Policy).Validate(ValidRequest() with { Roles = [WorkerRole.Courier, WorkerRole.Courier] });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Roles));
    }

    [Fact]
    public void UpdateWorker_UnknownRole_Fails()
    {
        var request = new UpdateWorkerRequest("jan@kamada.test", "Jan", "Kowalski", "+48 600 000 000", [(WorkerRole)42]);

        var result = new UpdateWorkerRequestValidator().Validate(request);

        result.Errors.ShouldContain(e => e.PropertyName == nameof(UpdateWorkerRequest.Roles));
    }

    [Fact]
    public void ChangePassword_SameAsCurrent_Fails()
    {
        var result = new ChangePasswordRequestValidator(Policy).Validate(new ChangePasswordRequest("Secret-pass-2026", "Secret-pass-2026"));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ChangePasswordRequest.NewPassword));
    }

    [Fact]
    public void ChangePassword_StrongNewPassword_Passes()
    {
        new ChangePasswordRequestValidator(Policy).Validate(new ChangePasswordRequest("Secret-pass-2026", "Nowe-haslo-do-testow")).IsValid.ShouldBeTrue();
    }
}
