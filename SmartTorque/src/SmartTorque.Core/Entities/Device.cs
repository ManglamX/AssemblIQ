using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A registered measurement hardware device (Torque wrench or Vernier caliper)
/// communicating via ESP32 gateway (Architecture §5.3).
/// </summary>
public class Device
{
    public int DeviceId { get; set; }

    /// <summary>Unique hardware transmitter ID, e.g. "TORQUE-01" or MAC address.</summary>
    public string TransmitterId { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>1 Torque, 2 Vernier.</summary>
    public InstrumentType InstrumentType { get; set; }

    public string? Location { get; set; }

    public string? FirmwareVersion { get; set; }

    /// <summary>SHA-256 hashed API key used to authenticate device requests (Architecture §8.4).</summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    public DateTime? LastSeenAt { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public bool IsOnline => LastSeenAt.HasValue && (DateTime.UtcNow - LastSeenAt.Value).TotalSeconds < 30;

    public DateOnly? CalibrationDue { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    // Navigation
    public ICollection<Session> TorqueSessions { get; set; } = new List<Session>();

    public ICollection<Session> VernierSessions { get; set; } = new List<Session>();

    public ICollection<SessionResult> SessionResults { get; set; } = new List<SessionResult>();
}