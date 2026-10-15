using Microsoft.AspNetCore.Identity;

using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Extended Identity user with plant-specific fields and registration approval state.
/// </summary>
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;

    public string? EmployeeNo { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Pending, Approved, Rejected</summary>
    public RegistrationStatus RegistrationStatus { get; set; } = RegistrationStatus.Approved;

    /// <summary>Role requested during self-registration: "Worker" or "Admin"</summary>
    public string? RequestedRole { get; set; }

    /// <summary>Reason provided by administrator if rejected</summary>
    public string? RejectionReason { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string? ReviewedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}