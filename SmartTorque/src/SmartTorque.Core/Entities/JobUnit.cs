using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Represents one physical unit/instance of a Job with an individual 40-character unique tracking token.
/// </summary>
public class JobUnit
{
    public int JobUnitId { get; set; }

    public int JobId { get; set; }

    /// <summary>1-based index of this unit within the Job (e.g. Unit #1 of 10).</summary>
    public int UnitIndex { get; set; }

    /// <summary>Unique 40-character tracking token assigned to this specific unit.</summary>
    public string TokenNo { get; set; } = string.Empty;

    /// <summary>Optional per-unit serial number.</summary>
    public string? ProductSerial { get; set; }

    public UnitAssemblyStatus Status { get; set; } = UnitAssemblyStatus.Pending;

    public DateTime? StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string? CompletedByWorkerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Job Job { get; set; } = null!;

    public ApplicationUser? CompletedByWorker { get; set; }
}
