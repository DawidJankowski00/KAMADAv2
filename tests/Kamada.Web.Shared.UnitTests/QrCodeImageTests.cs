using Kamada.Web.Shared.Account;

namespace Kamada.Web.Shared.UnitTests;

public sealed class QrCodeImageTests
{
    [Fact]
    public void ToDataUrl_ReturnsAPngImage()
    {
        var url = QrCodeImage.ToDataUrl("otpauth://totp/KAMADA:anna?secret=ABCDEFGHIJKLMNOP&issuer=KAMADA");

        url.ShouldStartWith("data:image/png;base64,");
        var png = Convert.FromBase64String(url["data:image/png;base64,".Length..]);
        png[..8].ShouldBe(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
    }
}
