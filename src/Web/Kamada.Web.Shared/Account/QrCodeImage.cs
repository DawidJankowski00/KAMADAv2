using QRCoder;

namespace Kamada.Web.Shared.Account;

/// <summary>
/// Draws a QR code in the browser (QRCoder, MIT): the 2FA secret never leaves the app for a QR service.
/// </summary>
public static class QrCodeImage
{
    /// <summary>PNG as a <c>data:</c> URL for an <c>img</c> tag.</summary>
    public static string ToDataUrl(string content)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.M);
        var png = new PngByteQRCode(data).GetGraphic(pixelsPerModule: 6);
        return "data:image/png;base64," + Convert.ToBase64String(png);
    }
}
