using QRCoder;

namespace SmartTorque.Services;

public class QrCodeService : IQrCodeService
{
    public string GenerateSvg(string text, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qr = new SvgQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }

    public byte[] GeneratePng(string text, int pixelsPerModule = 10)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var qr = new PngByteQRCode(data);
        return qr.GetGraphic(pixelsPerModule);
    }
}