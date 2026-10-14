using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// One uploaded BM Excel file. Holds the parsing outcome so the Admin can
/// preview the rows before any Job is created (Architecture §7.3).
/// </summary>
public class BmUpload
{
    public int UploadId { get; set; }

    /// <summary>Original file name as chosen by the Admin.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>Relative path under wwwroot/uploads/bm/.</summary>
    public string StoredPath { get; set; } = string.Empty;

    public string? UploadedBy { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Uploaded → Previewed → Confirmed (or Failed).</summary>
    public BmUploadStatus Status { get; set; } = BmUploadStatus.Uploaded;

    public int RowCount { get; set; }

    public int ErrorCount { get; set; }

    // Navigation
    public ICollection<BmRow> Rows { get; set; } = new List<BmRow>();
}