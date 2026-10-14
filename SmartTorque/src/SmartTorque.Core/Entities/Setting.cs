namespace SmartTorque.Core.Entities;

/// <summary>
/// Key/value settings table. Seeded with defaults.
/// </summary>
public class Setting
{
    public int SettingId { get; set; }

    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? UpdatedBy { get; set; }
}