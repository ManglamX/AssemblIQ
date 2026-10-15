using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// An active or completed worker measurement session on one component instance of a unit (Architecture §6).
/// </summary>
public class Session
{
    public long SessionId { get; set; }

    public int JobId { get; set; }

    public int ProductComponentId { get; set; }

    /// <summary>1..Job.Quantity</summary>
    public int UnitNo { get; set; } = 1;

    /// <summary>1..ProductComponent.Quantity</summary>
    public int ComponentInstanceNo { get; set; } = 1;

    public string WorkerId { get; set; } = string.Empty;

    public int? TorqueDeviceId { get; set; }

    public int? VernierDeviceId { get; set; }

    public DateTime StartTime { get; set; } = DateTime.UtcNow;

    public DateTime? EndTime { get; set; }

    public SessionStatus Status { get; set; } = SessionStatus.InProgress;

    public SmartTorque.Core.Enums.SessionResult FinalResult { get; set; } = SmartTorque.Core.Enums.SessionResult.Incomplete;

    /// <summary>Sequence of the next step to execute (for resuming after browser reload).</summary>
    public int CurrentStepSequence { get; set; } = 1;

    public int ReworkCount { get; set; }

    public string? Remarks { get; set; }

    // Navigation
    public Job Job { get; set; } = null!;

    public ProductComponent ProductComponent { get; set; } = null!;

    public ApplicationUser Worker { get; set; } = null!;

    public Device? TorqueDevice { get; set; }

    public Device? VernierDevice { get; set; }

    public ICollection<SessionResult> Results { get; set; } = new List<SessionResult>();
}