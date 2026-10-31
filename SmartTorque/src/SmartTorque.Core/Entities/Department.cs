namespace SmartTorque.Core.Entities;

/// <summary>
/// Operating department within a Plant (e.g. "Mechanical Assembly", "Fabrication", "Electrical").
/// Department personnel and workers are strictly scoped to their department (no cross-seeing).
/// </summary>
public class Department
{
    public int DepartmentId { get; set; }

    public int PlantId { get; set; }

    public Plant Plant { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<AssemblyLine> ProductionUnits { get; set; } = new List<AssemblyLine>();

    public ICollection<Product> Products { get; set; } = new List<Product>();

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
