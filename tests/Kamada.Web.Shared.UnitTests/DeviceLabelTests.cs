using Kamada.Web.Shared.Account;

namespace Kamada.Web.Shared.UnitTests;

public sealed class DeviceLabelTests
{
    [Theory]
    [InlineData("Mozilla/5.0 (Linux; Android 15; Pixel 9) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Mobile Safari/537.36", "Chrome · Android")]
    [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/140.0 Safari/537.36 Edg/140.0", "Edge · Windows")]
    [InlineData("Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1", "Safari · iPhone")]
    [InlineData("Mozilla/5.0 (X11; Linux x86_64; rv:140.0) Gecko/20100101 Firefox/140.0", "Firefox · Linux")]
    [InlineData("curl/8.5.0", "Nieznana przeglądarka")]
    [InlineData(null, "Nieznane urządzenie")]
    public void Describe_ReturnsBrowserAndSystem(string? userAgent, string expected)
    {
        DeviceLabel.Describe(userAgent).ShouldBe(expected);
    }

    [Fact]
    public void IsPhone_AndroidOrMobile_True_Desktop_False()
    {
        DeviceLabel.IsPhone("Mozilla/5.0 (Linux; Android 15) Chrome/140.0 Mobile").ShouldBeTrue();
        DeviceLabel.IsPhone("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/140.0").ShouldBeFalse();
    }
}
