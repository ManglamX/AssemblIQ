namespace SmartTorque.Core.Entities;

/// <summary>
/// An API key stored in the centralized system directory visible to the Top/Main Admin.
/// </summary>
public class ApiKeyDirectoryEntry
{
    public int ApiKeyId { get; set; }

    /// <summary>Human-readable name, e.g. "Readings Ingestion API", "ESP32 Torque Device API".</summary>
    public string KeyName { get; set; } = string.Empty;

    /// <summary>Scope or endpoint the API key authorizes, e.g. "/api/readings", "Device Management", "BM Integration".</summary>
    public string ApiScope { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>The raw API key string.</summary>
    public string RawApiKey { get; set; } = string.Empty;

    /// <summary>SHA-256 hash of the API key for verification.</summary>
    public string ApiKeyHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string CreatedBy { get; set; } = string.Empty;

    // Navigation
    public List<ApiKeyAccessGrant> Grants { get; set; } = [];
}
