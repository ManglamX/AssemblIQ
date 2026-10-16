using System.ComponentModel.DataAnnotations;
using SmartTorque.Core.Entities;

namespace SmartTorque.Web.ViewModels.ApiKeys;

public class ApiKeysDashboardViewModel
{
    public bool IsMainAdmin { get; set; }

    public string CurrentUserEmail { get; set; } = string.Empty;

    // Main Admin Views
    public IReadOnlyList<ApiKeyDirectoryEntry> AllKeys { get; set; } = [];
    public IReadOnlyList<ApiKeyAccessGrant> PendingRequests { get; set; } = [];
    public IReadOnlyList<ApiKeyAccessGrant> AllGrants { get; set; } = [];

    // Regular Admin Views
    public IReadOnlyList<ApiKeyAccessGrant> ApprovedGrants { get; set; } = [];
    public IReadOnlyList<ApiKeyAccessGrant> MyRequests { get; set; } = [];
    public IReadOnlyList<ApiKeyDirectoryEntry> AvailableKeys { get; set; } = [];
}

public class CreateApiKeyInputModel
{
    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, StringLength(100)]
    public string Scope { get; set; } = string.Empty;

    [Required, StringLength(500)]
    public string Description { get; set; } = string.Empty;

    [StringLength(128)]
    public string? RawApiKey { get; set; }
}

public class RequestApiKeyInputModel
{
    [Required]
    public int ApiKeyId { get; set; }

    [Required, StringLength(500)]
    public string Reason { get; set; } = string.Empty;
}

public class RejectApiKeyInputModel
{
    [Required]
    public int GrantId { get; set; }

    [StringLength(500)]
    public string? RejectionReason { get; set; }
}
