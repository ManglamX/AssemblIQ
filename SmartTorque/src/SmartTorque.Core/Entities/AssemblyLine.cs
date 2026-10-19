namespace SmartTorque.Core.Entities;

/// <summary>
/// An assembly line within a manufacturing department.
/// Connects products, workers, and tasks for workload-based execution.
/// </summary>
public class AssemblyLine
{
    public int AssemblyLineId { get; set; }

    /// <summary>
    /// Unique identifier / code for the assembly line (e.g. "ASM-01", "LINE-A").
    /// Serves as the primary reference across products, workers, and tasks.
    /// </summary>
    public string AssemblyCode { get; set; } = string.Empty;

    /// <summary>Human-readable name of the assembly line (e.g. "Chassis Final Assembly Line").</summary>
    public string AssemblyName { get; set; } = string.Empty;

    /// <summary>Department to which the assembly line belongs (e.g. "Fabrication", "Mechanical Assembly").</summary>
    public string Department { get; set; } = "Assembly";

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
}
