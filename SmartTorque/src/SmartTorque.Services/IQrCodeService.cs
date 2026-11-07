namespace SmartTorque.Services;

public interface IQrCodeService
{
    string GenerateSvg(string text, int pixelsPerModule = 10);
    byte[] GeneratePng(string text, int pixelsPerModule = 10);
}
