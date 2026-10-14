namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Stores uploaded BM workbooks under {WebRoot}/uploads/bm.
/// Validates: .xlsx only, ≤ 10 MB. Returns the relative web path
/// "uploads/bm/{guid}.xlsx".
/// </summary>
public interface IBmFileStore
{
    Task<string> SaveAsync(
        Stream stream,
        string originalFileName,
        long fileSizeBytes,
        CancellationToken ct = default);

    /// <summary>Opens a stored workbook, or null when it is missing.</summary>
    Stream? OpenRead(string relativePath);

    void Delete(string? relativePath);
}