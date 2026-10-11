using Kamada.Web.Shared.Api;

namespace Kamada.Web.Shared.UnitTests;

public sealed class ErrorMessagesTests
{
    [Theory]
    [InlineData("Delivery.RouteNotPlanned")]
    [InlineData("Delivery.StopsWithoutCoordinates")]
    [InlineData("Delivery.RunNotEditable")]
    [InlineData("Workers.CourierInactive")]
    [InlineData("Workers.CannotRemoveOwnAdminRole")]
    public void TryTranslate_KnownCodes_HavePolishMessages(string code) =>
        ErrorMessages.TryTranslate(code).ShouldNotBeNullOrWhiteSpace();

    [Fact]
    public void TryTranslate_UnknownCode_ReturnsNull() =>
        ErrorMessages.TryTranslate("Nope.Unknown").ShouldBeNull();
}
