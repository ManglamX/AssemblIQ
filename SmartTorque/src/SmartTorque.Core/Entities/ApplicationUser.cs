using Microsoft.AspNetCore.Identity;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Extended Identity user with plant-specific fields.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string? EmployeeNo { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}