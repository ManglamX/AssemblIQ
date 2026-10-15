using System.ComponentModel.DataAnnotations;

using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Template;

// -------------------------------------------------------
// Stage 1: Basic Info
// -------------------------------------------------------

public class Stage1ViewModel
{
    [Display(Name = "Template Code")]
    public string? TemplateCode { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(200, ErrorMessage = "Name must be ≤ 200 characters")]
    [Display(Name = "Template Name")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000, ErrorMessage = "Description must be ≤ 1000 characters")]
    [Display(Name = "Description (optional)")]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Mode is required")]
    [Display(Name = "Mode")]
    public TemplateMode Mode { get; set; }

    /// <summary>Set by controller after code-uniqueness check; shown in UI.</summary>
    public bool CodeAlreadyExists { get; set; }

    /// <summary>Populated when editing — existing templateId being edited.</summary>
    public int? EditingTemplateId { get; set; }
}

// -------------------------------------------------------
// Stage 2: Image & Step Count
// -------------------------------------------------------

public class Stage2ViewModel
{
    // Passed through from Stage 1
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateMode Mode { get; set; }
    public int? EditingTemplateId { get; set; }

    [Range(1, 100, ErrorMessage = "Steps must be between 1 and 100")]
    [Display(Name = "Number of Steps")]
    public int StepCount { get; set; } = 1;

    /// <summary>Relative web path set after upload.</summary>
    public string? ImagePath { get; set; }

    /// <summary>True when user already uploaded an image (either new or kept from edit).</summary>
    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
}

// -------------------------------------------------------
// Stage 3: Per-Step Values & Tolerances
// -------------------------------------------------------

public class Stage3ViewModel
{
    // Passed through from stages 1 & 2
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateMode Mode { get; set; }
    public string? ImagePath { get; set; }
    public int? EditingTemplateId { get; set; }

    public List<StepRowViewModel> Steps { get; set; } = [];

    /// <summary>Generated deterministic template code based on instrument specifications.</summary>
    public string? GeneratedTemplateCode { get; set; }

    /// <summary>Populated when an existing template with the generated code already exists.</summary>
    public MatchingTemplateViewModel? ExistingMatchingTemplate { get; set; }
}

public class MatchingTemplateViewModel
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateMode Mode { get; set; }
    public int Version { get; set; }
    public int StepCount { get; set; }
    public List<MatchingStepViewModel> Steps { get; set; } = [];
}

public class MatchingStepViewModel
{
    public int SequenceNo { get; set; }
    public string Label { get; set; } = string.Empty;
    public InstrumentType Instrument { get; set; }
    public decimal Target { get; set; }
    public decimal TolerancePlus { get; set; }
    public decimal ToleranceMinus { get; set; }
    public ToleranceMode ToleranceMode { get; set; }
    public string Unit => Instrument == InstrumentType.Torque ? "N·m" : "mm";
}

public class GenerateCodeRequest
{
    public TemplateMode Mode { get; set; }
    public int? EditingTemplateId { get; set; }
    public List<StepRowViewModel> Steps { get; set; } = [];
}

public class StepRowViewModel
{
    public int SequenceNo { get; set; }

    [Required(ErrorMessage = "Instrument is required")]
    public InstrumentType Instrument { get; set; }

    [Required(ErrorMessage = "Label is required")]
    [MaxLength(200)]
    [Display(Name = "Label")]
    public string Label { get; set; } = string.Empty;

    [Range(0.001, 9999999, ErrorMessage = "Target must be > 0")]
    [Display(Name = "Target")]
    public decimal Target { get; set; }

    [Range(0, 9999999, ErrorMessage = "Tolerance must be ≥ 0")]
    [Display(Name = "Tol +")]
    public decimal TolerancePlus { get; set; }

    [Range(0, 9999999, ErrorMessage = "Tolerance must be ≥ 0")]
    [Display(Name = "Tol −")]
    public decimal ToleranceMinus { get; set; }

    [Display(Name = "Tol Mode")]
    public ToleranceMode ToleranceMode { get; set; } = ToleranceMode.Absolute;

    [Range(1, 20, ErrorMessage = "Max retries 1–20")]
    [Display(Name = "Max Retries")]
    public int MaxRetries { get; set; } = 3;

    /// <summary>Percentage X (0–100). Null = marker not placed.</summary>
    public decimal? MarkerX { get; set; }
    public decimal? MarkerY { get; set; }

    // Derived display helpers
    public string Unit => Instrument == InstrumentType.Torque ? "N·m" : "mm";
}

// -------------------------------------------------------
// Preview / List
// -------------------------------------------------------

public class TemplateListViewModel
{
    public List<TemplateRowViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Filter state
    public string? Search { get; set; }
    public TemplateMode? ModeFilter { get; set; }
    public InstrumentType? InstrumentFilter { get; set; }
    public bool? ActiveFilter { get; set; }
}

public class TemplateRowViewModel
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public TemplateMode Mode { get; set; }
    public int Version { get; set; }
    public int StepCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TemplateDetailViewModel
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TemplateMode Mode { get; set; }
    public string? ImagePath { get; set; }
    public int Version { get; set; }
    public bool IsLatest { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<StepDetailViewModel> Steps { get; set; } = [];
    public List<VersionRowViewModel> OtherVersions { get; set; } = [];
}

public class StepDetailViewModel
{
    public int SequenceNo { get; set; }
    public string Label { get; set; } = string.Empty;
    public InstrumentType Instrument { get; set; }
    public string Unit => Instrument == InstrumentType.Torque ? "N·m" : "mm";
    public decimal Target { get; set; }
    public decimal Lower { get; set; }
    public decimal Upper { get; set; }
    public ToleranceMode ToleranceMode { get; set; }
    public int MaxRetries { get; set; }
    public decimal? MarkerX { get; set; }
    public decimal? MarkerY { get; set; }
}

public class VersionRowViewModel
{
    public int TemplateId { get; set; }
    public int Version { get; set; }
    public bool IsLatest { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? CreatedBy { get; set; }
}