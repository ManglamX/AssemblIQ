using SmartTorque.Core.Interfaces;

namespace SmartTorque.Services;

/// <summary>
/// Saves BM workbooks under {WebRoot}/uploads/bm.
/// Validates: .xlsx only, ≤ 10 MB. Returns relative web path: "uploads/bm/{guid}.xlsx".
/// </summary>
public sealed class BmFileStore : IBmFileStore
{
    private const long MaxBytes = 10 * 1024 * 1024; // 10 MB
    private const string Subfolder = "bm";
    private const string Extension = ".xlsx";

    private readonly string _webRoot;

    /// <param name="webRootPath">
    /// The absolute path to wwwroot — injected as a string so the Services project
    /// doesn't reference Microsoft.AspNetCore.Hosting. Register with
    /// services.AddSingleton(env.WebRootPath).
    /// </param>
    public BmFileStore(string webRootPath)
    {
        _webRoot = webRootPath;
    }

    public async Task<string> SaveAsync(
        Stream stream,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken ct = default)
    {
        if (fileSizeBytes <= 0)
            throw new InvalidOperationException("The file is empty.");

        if (fileSizeBytes > MaxBytes)
            throw new InvalidOperationException(
                $"The workbook must be ≤ 10 MB. Received {fileSizeBytes / 1024.0 / 1024.0:F1} MB.");

        // .xlsx only — the parser reads ClosedXML, so .xls would only fail later.
        if (!string.Equals(Path.GetExtension(originalFileName), Extension, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Only .xlsx workbooks can be imported.");

        var folder = Path.Combine(_webRoot, "uploads", Subfolder);
        Directory.CreateDirectory(folder);

        var fileName = $"{Guid.NewGuid()}{Extension}";
        var fullPath = Path.Combine(folder, fileName);

        await using var fs = File.Create(fullPath);
        await stream.CopyToAsync(fs, ct);

        return $"uploads/{Subfolder}/{fileName}";
    }

    public Stream? OpenRead(string relativePath)
    {
        var fullPath = Resolve(relativePath);
        return fullPath != null && File.Exists(fullPath) ? File.OpenRead(fullPath) : null;
    }

    public void Delete(string? relativePath)
    {
        var fullPath = Resolve(relativePath);
        if (fullPath != null && File.Exists(fullPath)) File.Delete(fullPath);
    }

    /// <summary>Resolves a stored path, refusing anything outside the uploads root.</summary>
    private string? Resolve(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)) return null;

        var normalized = relativePath
            .Replace('/', Path.DirectorySeparatorChar)
            .Replace('\\', Path.DirectorySeparatorChar);

        var fullPath = Path.GetFullPath(Path.Combine(_webRoot, normalized));
        var uploadsRoot = Path.GetFullPath(Path.Combine(_webRoot, "uploads"));

        return fullPath.StartsWith(uploadsRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : null;
    }
}