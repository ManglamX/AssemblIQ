using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// One parsed row of a BM sheet. Colours are detected and mapped to a priority
/// at parse time; the Admin can override <see cref="Priority"/> and
/// <see cref="ImportStatus"/> in the preview screen (PRD BM-4, BM-5).
/// </summary>
public class BmRow
{
    public int RowId { get; set; }

    public int UploadId { get; set; }

    /// <summary>Excel row number, used to point the Admin at the offending cell.</summary>
    public int ExcelRowNo { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public int Quantity { get; set; } = 1;

    public DateOnly? DueDate { get; set; }

    /// <summary>Detected fill of the ProductCode cell, `#RRGGBB` (or null when none).</summary>
    public string? ColourHex { get; set; }

    /// <summary>Mapped priority; defaults to White until detection assigns one.</summary>
    public PriorityLevel Priority { get; set; } = PriorityLevel.White;

    public string? Remarks { get; set; }

    public BmRowImportStatus ImportStatus { get; set; } = BmRowImportStatus.Ok;

    /// <summary>Why the row is in Error/Warning, shown in the preview.</summary>
    public string? ImportMessage { get; set; }

    // Navigation
    public BmUpload Upload { get; set; } = null!;
}