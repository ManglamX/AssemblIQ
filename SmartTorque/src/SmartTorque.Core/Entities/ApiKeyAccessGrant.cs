using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A persistent record of an Admin's request and granted access to an API key from the central directory.
/// </summary>
public class ApiKeyAccessGrant
{
    public int GrantId { get; set; }

    public int ApiKeyId { get; set; }

    /// <summary>The Admin user requesting access.</summary>
    public string UserId { get; set; } = string.Empty;

    public ApiKeyRequestStatus Status { get; set; } = ApiKeyRequestStatus.Pending;

    /// <summary>Justification provided by the requesting Admin.</summary>
    public string? RequestReason { get; set; }

    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DecidedAt { get; set; }

    public string? DecidedByUserId { get; set; }

    public string? RejectionReason { get; set; }

    // Navigation
    public ApiKeyDirectoryEntry ApiKey { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;

    public ApplicationUser? DecidedByUser { get; set; }
}