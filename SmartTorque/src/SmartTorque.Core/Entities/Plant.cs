namespace SmartTorque.Core.Entities;

/// <summary>
/// Manufacturing facility belonging to a Company (e.g. "Plant 1 - Vikhroli").
/// Plant Heads/Main Admins have full visibility across all departments in their plant.
/// </summary>
public class Plant
{
    public int PlantId { get; set; }

    public int CompanyId { get; set; }

    public Company Company { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Location { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Department> Departments { get; set; } = new List<Department>();

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
