using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// An immutable, append-only record of a measurement attempt within a session (Architecture §5.3, §5.5).
/// Holds a snapshot of the target tolerances and computed limits at the moment of measurement.
/// </summary>
public class SessionResult
{
    public long ResultId { get; set; }

    public long SessionId { get; set; }

    public int StepId { get; set; }

    /// <summary>Instrument type snapshot: 1 Torque, 2 Vernier.</summary>
    public InstrumentType InstrumentType { get; set; }

    /// <summary>Transmitter device that generated the value (null only for manual entry).</summary>
    public int? DeviceId { get; set; }

    /// <summary>1-based attempt counter for this step within the session.</summary>
    public int AttemptNo { get; set; } = 1;

    /// <summary>Snapshot of target value from TemplateStep.</summary>
    public decimal TargetValue { get; set; }

    /// <summary>Snapshot of tolerance plus from TemplateStep.</summary>
    public decimal TolerancePlus { get; set; }

    /// <summary>Snapshot of tolerance minus from TemplateStep.</summary>
    public decimal ToleranceMinus { get; set; }

    /// <summary>Snapshot of tolerance mode: Absolute or Percent.</summary>
    public ToleranceMode ToleranceMode { get; set; } = ToleranceMode.Absolute;

    /// <summary>Snapshot of computed lower acceptance limit.</summary>
    public decimal LowerLimit { get; set; }

    /// <summary>Snapshot of computed upper acceptance limit.</summary>
    public decimal UpperLimit { get; set; }

    /// <summary>Actual measured value reported by device or operator.</summary>
    public decimal ActualValue { get; set; }

    /// <summary>1 Pass, 2 Fail.</summary>
    public StepResult Result { get; set; }

    public bool IsManualEntry { get; set; }

    public string? ManualApprovedBy { get; set; }

    public string? ReasonCode { get; set; }

    /// <summary>Device hardware sequence number used for deduplication/idempotency.</summary>
    public long? DeviceSeq { get; set; }

    /// <summary>Timestamp reported by the device (UTC).</summary>
    public DateTime CapturedAt { get; set; }

    /// <summary>Timestamp recorded by the server upon receipt (UTC).</summary>
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Session Session { get; set; } = null!;

    public TemplateStep Step { get; set; } = null!;

    public Device? Device { get; set; }
}