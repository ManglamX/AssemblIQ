using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// An assembly line or production cell within a manufacturing department.
/// Connects products, workers, and tasks for workload-based execution.
/// </summary>
public class AssemblyLine
{
    public int AssemblyLineId { get; set; }

    /// <summary>
    /// Unique identifier / code for the assembly line or cell (e.g. "ASM-01", "LINE-A", "CELL-01").
    /// Serves as the primary reference across products, workers, and tasks.
    /// </summary>
    public string AssemblyCode { get; set; } = string.Empty;

    /// <summary>Human-readable name of the assembly line (e.g. "Chassis Final Assembly Line").</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>Department foreign key.</summary>
    public int? DepartmentId { get; set; }

    /// <summary>Department navigation entity.</summary>
    public Department? DepartmentNavigation { get; set; }

    /// <summary>Department name string (e.g. "Fabrication", "Mechanical Assembly") kept for backwards compatibility.</summary>
    public string Department { get; set; } = "Assembly";

    /// <summary>Production unit type: Line or Cell.</summary>
    public ProductionUnitType UnitType { get; set; } = ProductionUnitType.Line;

    /// <summary>Optional description and operating notes.</summary>
    public string? Description { get; set; }

    /// <summary>Status: Active or Inactive.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    // Navigation collections
    public ICollection<ProductAssemblyMapping> ProductMappings { get; set; } = new List<ProductAssemblyMapping>();

    public ICollection<ApplicationUser> AssignedWorkers { get; set; } = new List<ApplicationUser>();

    public ICollection<Job> Jobs { get; set; } = new List<Job>();

    public ICollection<WorkStation> WorkStations { get; set; } = new List<WorkStation>();
}
