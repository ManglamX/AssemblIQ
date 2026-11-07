using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Records an individual rework iteration and audit log for a physical product unit.
/// Supports multiple rework cycles even after a unit initially passed (Architecture §6).
/// </summary>
public class JobUnitRework
{
    public int JobUnitReworkId { get; set; }

    public int JobUnitId { get; set; }

    public int JobId { get; set; }

    public int UnitIndex { get; set; }

    public string TokenNo { get; set; } = string.Empty;

    /// <summary>1-based rework iteration (1 for 1st rework, 2 for 2nd rework, etc.).</summary>
    public int ReworkIteration { get; set; } = 1;

    /// <summary>Reason for sending to rework / error description.</summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>e.g. "Torque / Fastening", "Mechanical / Alignment", "Surface / Cosmetic", "Quality Audit", "Other"</summary>
    public string? DefectCategory { get; set; }

    /// <summary>Specific component templates flagged for rework (comma-separated template IDs, or empty for all).</summary>
    public string? TargetTemplateIds { get; set; }

    public string InitiatedByUserId { get; set; } = string.Empty;

    public string InitiatedByUserName { get; set; } = string.Empty;

    public DateTime InitiatedAt { get; set; } = DateTime.UtcNow;

    public UnitAssemblyStatus Status { get; set; } = UnitAssemblyStatus.Rework;

    public DateTime? CompletedAt { get; set; }

    public string? CompletedByUserId { get; set; }

    public string? CompletedByUserName { get; set; }

    public string? ResolutionNotes { get; set; }

    public SmartTorque.Core.Enums.SessionResult? FinalResult { get; set; }

    // Navigation
    public JobUnit JobUnit { get; set; } = null!;

    public Job Job { get; set; } = null!;
}
