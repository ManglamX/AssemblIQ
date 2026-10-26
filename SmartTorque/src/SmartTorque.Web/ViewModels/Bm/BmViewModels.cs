using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Bm;

public class BmIndexViewModel
{
    public List<BmUploadRowViewModel> RecentUploads { get; set; } = [];

    /// <summary>Colour legend panel, shown beside the drop zone (Design §7.6).</summary>
    public List<PriorityColourMap> Legend { get; set; } = [];

    public bool HasLegend => Legend.Count > 0;

    public bool HasUploads => RecentUploads.Count > 0;

    /// <summary>Form data for creating an individual workload manually.</summary>
    public CreateManualWorkloadViewModel CreateWorkload { get; set; } = new();

    /// <summary>Active products available for selection.</summary>
    public List<ProductOptionViewModel> AvailableProducts { get; set; } = [];

    /// <summary>Active assembly lines available for selection.</summary>
    public List<AssemblyLineOptionViewModel> AvailableAssemblyLines { get; set; } = [];

    /// <summary>Active worker accounts for direct assignment.</summary>
    public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> AvailableWorkers { get; set; } = [];
}

public class ProductOptionViewModel
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? DefaultAssemblyLineId { get; set; }
    public string? DefaultAssemblyName { get; set; }
}

public class AssemblyLineOptionViewModel
{
    public string AssemblyCode { get; set; } = string.Empty;
    public int AssemblyLineId { get; set; }
    public string AssemblyName { get; set; } = string.Empty;
    public string? Department { get; set; }
}

public class CreateManualWorkloadViewModel
{
    [System.ComponentModel.DataAnnotations.Required(ErrorMessage = "Product / Item Code is required.")]
    public string ProductCode { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, 100000, ErrorMessage = "Quantity must be at least 1.")]
    public int Quantity { get; set; } = 1;

    public DateOnly? DueDate { get; set; }

    public PriorityLevel Priority { get; set; } = PriorityLevel.White;

    public string? SalesOrder { get; set; }

    public string? PONo { get; set; }

    public string? Remarks { get; set; }

    public int? AssemblyLineId { get; set; }

    public string? AssignedWorkerId { get; set; }

    [System.ComponentModel.DataAnnotations.Range(0.1, 1000.0, ErrorMessage = "Workload must be greater than 0.")]
    public decimal EstimatedWorkload { get; set; } = 1.0m;
}

public class BmUploadRowViewModel
{
    public int UploadId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string? UploadedBy { get; set; }

    public BmUploadStatus Status { get; set; }

    public int RowCount { get; set; }

    public int ErrorCount { get; set; }
}

public class BmPreviewViewModel
{
    public int UploadId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; }

    public string? UploadedBy { get; set; }

    public BmUploadStatus Status { get; set; }

    /// <summary>Present when the workbook could not be read at all.</summary>
    public List<string> FileErrors { get; set; } = [];

    public List<BmRowViewModel> Rows { get; set; } = [];

    public List<PriorityColourMap> Legend { get; set; } = [];

    public int OkCount => Rows.Count(r => r.ImportStatus == BmRowImportStatus.Ok);

    public int WarningCount => Rows.Count(r => r.ImportStatus == BmRowImportStatus.Warning);

    public int ErrorCount => Rows.Count(r => r.ImportStatus == BmRowImportStatus.Error);

    public int SkippedCount => Rows.Count(r => r.ImportStatus == BmRowImportStatus.Skipped);

    /// <summary>Rows that would become Jobs once the Admin confirms (P4.6).</summary>
    public int ImportableCount => Rows.Count(r => r.CanSkip);

    /// <summary>Job numbers created from this upload, shown once it is imported.</summary>
    public List<string> JobNumbers { get; set; } = [];

    /// <summary>Job numbers that were refreshed from this upload (BM-6).</summary>
    public List<string> RefreshedJobNumbers { get; set; } = [];

    /// <summary>Preview comparison of row matches and absent jobs (BM-6).</summary>
    public SmartTorque.Services.BmRefreshPreviewResult? RefreshPreview { get; set; }

    /// <summary>Open jobs in the system that are absent from this workbook (Architecture §7.3 step 6).</summary>
    public IReadOnlyList<Job> AbsentOpenJobs => RefreshPreview?.AbsentOpenJobs ?? [];

    /// <summary>Count of rows that will refresh existing open jobs.</summary>
    public int RefreshedCount => RefreshPreview?.MatchedRows.Count ?? 0;

    /// <summary>Count of rows that will create new jobs.</summary>
    public int NewJobCount => Math.Max(0, ImportableCount - RefreshedCount);

    public bool IsFailed => Status == BmUploadStatus.Failed;

    public bool IsConfirmed => Status == BmUploadStatus.Confirmed;

    public bool CanEdit => Status == BmUploadStatus.Previewed;

    /// <summary>Design §7.6: primary `Confirm import` next to the secondary `Cancel`.</summary>
    public bool CanConfirm => CanEdit && ImportableCount > 0;

    public string Summary => SkippedCount > 0
        ? $"{Rows.Count} rows · {OkCount} OK · {WarningCount} warnings · {ErrorCount} errors · {SkippedCount} skipped"
        : $"{Rows.Count} rows · {OkCount} OK · {WarningCount} warnings · {ErrorCount} errors";
}

public class BmRowViewModel
{
    public int RowId { get; set; }

    /// <summary>Matched open job if this row refreshes an existing open job (BM-6).</summary>
    public SmartTorque.Services.BmRowJobMatch? MatchedJob { get; set; }

    public bool IsRefresh => MatchedJob != null;

    /// <summary>Excel row number, so the Admin can find it in the sheet.</summary>
    public int ExcelRowNo { get; set; }

    public string ProductCode { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public DateOnly? DueDate { get; set; }

    public string? Remarks { get; set; }

    public string? Description => Remarks;

    public string? SalesOrder { get; set; }

    public string? PONo { get; set; }

    public string? ColourHex { get; set; }

    public PriorityLevel Priority { get; set; }

    public int PriorityNumber => (int)Priority;

    public BmRowImportStatus ImportStatus { get; set; }

    public string? ImportMessage { get; set; }

    public bool IsError => ImportStatus == BmRowImportStatus.Error;

    public bool IsWarning => ImportStatus == BmRowImportStatus.Warning;

    public bool IsSkipped => ImportStatus == BmRowImportStatus.Skipped;

    /// <summary>Only rows that can be imported can be skipped, and then restored.</summary>
    public bool CanSkip => ImportStatus is BmRowImportStatus.Ok or BmRowImportStatus.Warning;

    /// <summary>Class that marks the row so errors and warnings stand out.</summary>
    public string RowClass => ImportStatus switch
    {
        BmRowImportStatus.Error => "table-row-fail",
        BmRowImportStatus.Warning => "table-row-warn",
        BmRowImportStatus.Skipped => "table-row-skip",
        _ => string.Empty
    };

    public string StatusBadgeClass => ImportStatus switch
    {
        BmRowImportStatus.Ok => "badge-completed",
        BmRowImportStatus.Warning => "badge-onhold",
        BmRowImportStatus.Error => "badge-rework",
        BmRowImportStatus.Imported => "badge-completed",
        BmRowImportStatus.Skipped => "badge-cancelled",
        _ => "badge-pending"
    };

    public string StatusLabel => ImportStatus switch
    {
        BmRowImportStatus.Ok => "Ready",
        BmRowImportStatus.Warning => "Warning",
        BmRowImportStatus.Error => "Error",
        BmRowImportStatus.Imported => "Imported",
        BmRowImportStatus.Skipped => "Skipped",
        _ => ImportStatus.ToString()
    };
}

public class BmMapColumnsViewModel
{
    public string TempFileId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public List<string> Headers { get; set; } = [];
    public string? DetectedItemCodeColumn { get; set; }
    public string? SelectedDescriptionColumn { get; set; }
    public string? SelectedQuantityColumn { get; set; }
    public string? SelectedDueDateColumn { get; set; }
    public string? DetectedSalesOrderColumn { get; set; }
    public string? DetectedPONoColumn { get; set; }
    public string? SelectedSalesOrderColumn { get; set; }
    public string? SelectedPONoColumn { get; set; }
}