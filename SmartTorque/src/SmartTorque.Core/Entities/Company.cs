namespace SmartTorque.Core.Entities;

/// <summary>
/// Root enterprise organization (e.g. "Godrej Enterprises").
/// </summary>
public class Company
{
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<Plant> Plants { get; set; } = new List<Plant>();

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
