using System.ComponentModel.DataAnnotations;

using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Product;

// -------------------------------------------------------
// List
// -------------------------------------------------------

public class ProductListViewModel
{
    public List<ProductRowViewModel> Items { get; set; } = [];
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalPages => (int)Math.Ceiling((double)TotalCount / PageSize);

    // Filter state
    public string? Search { get; set; }
    public bool? ActiveFilter { get; set; }
}

public class ProductRowViewModel
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int ComponentCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// -------------------------------------------------------
// Builder (create / edit) — Design.md §7.5
// -------------------------------------------------------

public class ProductBuilderViewModel
{
    /// <summary>Set when editing an existing product.</summary>
    public int? EditingProductId { get; set; }

    [Required(ErrorMessage = "Product code is required")]
    [RegularExpression(@"^[A-Z0-9\-_]{2,50}$",
        ErrorMessage = "Code must be 2–50 uppercase letters, digits, hyphens or underscores")]
    [Display(Name = "Product Code")]
    public string ProductCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(150, ErrorMessage = "Name must be ≤ 150 characters")]
    [Display(Name = "Product Name")]
    public string ProductName { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description must be ≤ 500 characters")]
    [Display(Name = "Description (optional)")]
    public string? Description { get; set; }

    /// <summary>Relative web path set after upload.</summary>
    public string? ImagePath { get; set; }

    /// <summary>Set by the controller after the uniqueness check; shown in the UI.</summary>
    public bool CodeAlreadyExists { get; set; }

    public List<ProductComponentRowViewModel> Components { get; set; } = [];

    /// <summary>Latest active templates offered in the picker.</summary>
    public List<TemplateOptionViewModel> AvailableTemplates { get; set; } = [];

    public bool HasImage => !string.IsNullOrEmpty(ImagePath);
}

public class ProductComponentRowViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Select a template")]
    public int TemplateId { get; set; }

    [Required(ErrorMessage = "Component name is required")]
    [MaxLength(100, ErrorMessage = "Component name must be ≤ 100 characters")]
    [Display(Name = "Component Name")]
    public string ComponentName { get; set; } = string.Empty;

    [Range(1, 9999, ErrorMessage = "Quantity must be at least 1")]
    [Display(Name = "Qty")]
    public int Quantity { get; set; } = 1;

    // Display only (not posted back meaningfully)
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public int StepCount { get; set; }
}

public class TemplateOptionViewModel
{
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Version { get; set; }
    public TemplateMode Mode { get; set; }
    public int StepCount { get; set; }
    public string Label => $"{TemplateCode} · {Name} · v{Version} · {StepCount} steps";
}

// -------------------------------------------------------
// Detail — Design.md §7.5/§7.7
// -------------------------------------------------------

public class ProductDetailViewModel
{
    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? ImagePath { get; set; }
    public bool IsActive { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public List<ProductComponentDetailViewModel> Components { get; set; } = [];

    public int TotalSteps => Components.Sum(c => c.StepCount);
    public int TotalQuantity => Components.Sum(c => c.Quantity);
}

public class ProductComponentDetailViewModel
{
    public int ProductComponentId { get; set; }
    public int AssemblyOrder { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int TemplateId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public int TemplateVersion { get; set; }
    public bool IsLatestVersion { get; set; }
    public TemplateMode Mode { get; set; }
    public int StepCount => Steps.Count;
    public List<ProductStepViewModel> Steps { get; set; } = [];
}

public class ProductStepViewModel
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
}

// -------------------------------------------------------
// Update-to-latest preview — Architecture §11
// -------------------------------------------------------

public class ComponentUpdateViewModel
{
    public int ProductComponentId { get; set; }
    public int ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int OldVersion { get; set; }
    public int NewVersion { get; set; }
    public bool IsAlreadyLatest { get; set; }
    public List<StepDiffViewModel> Steps { get; set; } = [];
    public bool HasChanges => Steps.Any(s => s.IsChanged) || Steps.Any(s => s.Old == null) || Steps.Any(s => s.New == null);
}

public class StepDiffViewModel
{
    public int SequenceNo { get; set; }
    public ProductStepViewModel? Old { get; set; }
    public ProductStepViewModel? New { get; set; }
    public bool IsChanged { get; set; }
}