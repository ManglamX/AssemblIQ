using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Tracks active time windows for worker sessions to enable timeline-windowed offline reconciliation (Architecture §7.8).
/// When Wi-Fi drops and an operator switches between tasks, readings are mapped to the exact session
/// that was active on the workstation during that timestamp window.
/// </summary>
public class SessionActivityWindow
{
    public long SessionActivityWindowId { get; set; }

    public long SessionId { get; set; }

    public int? DeviceId { get; set; }

    public string? WorkerId { get; set; }

    public DateTime WindowStart { get; set; } = DateTime.UtcNow;

    public DateTime? WindowEnd { get; set; }

    // Navigation
    public Session Session { get; set; } = null!;

    public Device? Device { get; set; }
}
