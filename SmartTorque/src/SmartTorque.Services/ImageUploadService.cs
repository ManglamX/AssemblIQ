using SmartTorque.Core.Interfaces;

namespace SmartTorque.Services;

/// <summary>
/// Saves images under {WebRoot}/uploads/{subfolder} (templates, products, …).
/// Validates: JPG/PNG only (by magic bytes), ≤ 5 MB.
/// Returns relative web path: "uploads/{subfolder}/{guid}.{ext}"
/// </summary>
public sealed class ImageUploadService : IImageUploadService
{
    // Magic bytes
    private static readonly byte[] JpgMagic = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngMagic = [0x89, 0x50, 0x4E, 0x47];

    private static readonly HashSet<string> AllowedExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png" };

    private const long MaxBytes = 5 * 1024 * 1024; // 5 MB
    private const string DefaultSubfolder = "templates";
    private readonly string _webRoot;

    /// <param name="webRootPath">
    /// The absolute path to wwwroot — injected as a string so the
    /// Services project doesn't reference Microsoft.AspNetCore.Hosting.
    /// Register with: services.AddSingleton(env.WebRootPath) in DI extensions.
    /// </param>
    public ImageUploadService(string webRootPath)
    {
        _webRoot = webRootPath;
    }

    public Task<string> SaveAsync(
        Stream imageStream,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken ct = default)
        => SaveAsync(imageStream, originalFileName, fileSizeBytes, DefaultSubfolder, ct);

    public async Task<string> SaveAsync(
        Stream imageStream,
        string originalFileName,
        long fileSizeBytes,
        string subfolder,
        CancellationToken ct = default)
    {
        // 1. Size check
        if (fileSizeBytes > MaxBytes)
            throw new InvalidOperationException(
                $"Image must be ≤ 5 MB. Received {fileSizeBytes / 1024.0 / 1024.0:F1} MB.");

        // 2. Extension check
        var ext = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            throw new InvalidOperationException(
                $"Only JPG and PNG files are allowed. Received: {ext}");

        // 3. Magic-byte check
        var header = new byte[4];
        var read = await imageStream.ReadAsync(header.AsMemory(0, 4), ct);
        if (read < 3 || !IsValidMagic(header, ext))
            throw new InvalidOperationException(
                "File content does not match a valid JPEG or PNG image.");

        imageStream.Seek(0, SeekOrigin.Begin);

        // 4. Save with GUID filename
        var safeSubfolder = SanitizeSubfolder(subfolder);
        var folder = Path.Combine(_webRoot, "uploads", safeSubfolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{ext}";
        var fullPath = Path.Combine(folder, fileName);
        await using var fs = File.Create(fullPath);
        await imageStream.CopyToAsync(fs, ct);

        return $"uploads/{safeSubfolder}/{fileName}";
    }

    public void Delete(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return;

        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(_webRoot, normalized));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_webRoot, "uploads"));

        // Guard against path traversal outside the uploads root.
        if (!fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            return;

        if (File.Exists(fullPath))
            File.Delete(fullPath);
    }

    private static string SanitizeSubfolder(string subfolder)
        => string.IsNullOrWhiteSpace(subfolder)
            ? DefaultSubfolder
            : string.Concat(subfolder.Where(c => char.IsLetterOrDigit(c) || c is '-' or '_'));

    private static bool IsValidMagic(byte[] header, string ext)
    {
        if (ext is ".jpg" or ".jpeg")
            return header[0] == JpgMagic[0] && header[1] == JpgMagic[1] && header[2] == JpgMagic[2];
        if (ext == ".png")
            return header[0] == PngMagic[0] && header[1] == PngMagic[1]
                && header[2] == PngMagic[2] && header[3] == PngMagic[3];
        return false;
    }
}