using System.ComponentModel.DataAnnotations;
using SmartTorque.Core.Enums;
using SmartTorque.Services;

namespace SmartTorque.Web.ViewModels.Assembly;

public class AssemblyIndexViewModel
{
    public AssemblyDashboardDto Dashboard { get; set; } = null!;
    public string? Search { get; set; }
    public string? Department { get; set; }
    public bool? IsActive { get; set; }
    public IReadOnlyList<string> Departments { get; set; } = [];
}

public class AssemblyLineFormViewModel
{
    public int? AssemblyLineId { get; set; }

    [Required(ErrorMessage = "Assembly ID is required.")]
    [StringLength(50, ErrorMessage = "Assembly ID cannot exceed 50 characters.")]
    [Display(Name = "Assembly ID / Code")]
    public string AssemblyCode { get; set; } = string.Empty;

    [Required(ErrorMessage = "Assembly Name is required.")]
    [StringLength(150, ErrorMessage = "Assembly Name cannot exceed 150 characters.")]
    [Display(Name = "Assembly Name")]
    public string AssemblyName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Department is required.")]
    [StringLength(100, ErrorMessage = "Department cannot exceed 100 characters.")]
    public string Department { get; set; } = "Assembly";

    [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters.")]
    public string? Description { get; set; }

    [Display(Name = "Active Status")]
    public bool IsActive { get; set; } = true;

    public bool IsEdit => AssemblyLineId.HasValue && AssemblyLineId.Value > 0;
}

public class ProductMappingsViewModel
{
    public IReadOnlyList<ProductWithLineDto> Products { get; set; } = [];
    public IReadOnlyList<AssemblyLineDto> AssemblyLines { get; set; } = [];
    public string? Search { get; set; }
    public int? AssemblyLineId { get; set; }
    public bool? UnmappedOnly { get; set; }
    public int UnmappedCount { get; set; }
}

public class WorkersManagementViewModel
{
    public IReadOnlyList<WorkerLineDto> Workers { get; set; } = [];
    public IReadOnlyList<AssemblyLineDto> AssemblyLines { get; set; } = [];
}
