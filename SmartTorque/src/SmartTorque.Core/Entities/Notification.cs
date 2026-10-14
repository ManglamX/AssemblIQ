using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A message shown on the worker board (e.g. a Black job was created).
/// Targeted at a role rather than a user, so every worker sees it.
/// </summary>
public class Notification
{
    public int NotificationId { get; set; }

    public NotificationType Type { get; set; } = NotificationType.General;

    public string Message { get; set; } = string.Empty;

    /// <summary>Nullable: some notifications are not job-specific.</summary>
    public int? JobId { get; set; }

    /// <summary>Role that should see it, e.g. "Worker".</summary>
    public string TargetRole { get; set; } = "Worker";

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    // Navigation
    public Job? Job { get; set; }
}