using Kamada.Modules.Workforce.Application.Workers;
using Kamada.Shared.Contracts.Workforce;

namespace Kamada.Modules.Workforce.UnitTests;

public sealed class ValidatorTests
{
    private static CreateWorkerRequest ValidRequest() =>
        new("kurier9", "Secret123", "Jan", "Kowalski", "+48 600 000 000", WorkerRole.Courier);

    [Fact]
    public void CreateWorker_ValidRequest_Passes()
    {
        new CreateWorkerRequestValidator().Validate(ValidRequest()).IsValid.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Short1A")]
    [InlineData("alllowercase1")]
    [InlineData("ALLUPPERCASE1")]
    [InlineData("NoDigitsHere")]
    public void CreateWorker_WeakPassword_Fails(string password)
    {
        var result = new CreateWorkerRequestValidator().Validate(ValidRequest() with { Password = password });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Password));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("has space")]
    [InlineData("zażółć")]
    public void CreateWorker_InvalidUsername_Fails(string username)
    {
        var result = new CreateWorkerRequestValidator().Validate(ValidRequest() with { Username = username });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.Username));
    }

    [Theory]
    [InlineData("123")]
    [InlineData("phone-number")]
    public void CreateWorker_InvalidPhone_Fails(string phone)
    {
        var result = new CreateWorkerRequestValidator().Validate(ValidRequest() with { PhoneNumber = phone });

        result.Errors.ShouldContain(e => e.PropertyName == nameof(CreateWorkerRequest.PhoneNumber));
    }
}
