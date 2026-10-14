namespace SmartTorque.Core.Entities;

/// <summary>
/// Immutable audit trail for admin actions. Written by the EF SaveChanges interceptor (Phase 8).
/// Table created now so foreign keys can reference it later.
/// </summary>
public class AuditLog
{
    public long AuditLogId { get; set; }

    public DateTime OccurredAt { get; set; } = DateTime.UtcNow;

    public string? UserId { get; set; }

    public string? UserName { get; set; }

    /// <summary>e.g. "Template", "Product", "Job"</summary>
    public string EntityName { get; set; } = string.Empty;

    /// <summary>String representation of the PK (could be int, Guid, etc.)</summary>
    public string? EntityId { get; set; }

    /// <summary>e.g. "Created", "Updated", "Deactivated", "VersionCreated"</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>JSON snapshot of changed properties. Null for deletes/deactivates.</summary>
    public string? Changes { get; set; }
}