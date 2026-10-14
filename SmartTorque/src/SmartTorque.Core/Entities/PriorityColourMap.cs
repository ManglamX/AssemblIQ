using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Config table mapping a BM row colour to a priority (PRD BM-3).
/// Editable by Admin; matched by nearest RGB distance (Architecture §7.3).
/// </summary>
public class PriorityColourMap
{
    public int MapId { get; set; }

    /// <summary>e.g. "#000000".</summary>
    public string ColourHex { get; set; } = string.Empty;

    public PriorityLevel Priority { get; set; }

    /// <summary>Human label shown as the badge: Black / Red / Yellow / Green / White.</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>RGB distance allowed when matching a detected colour.</summary>
    public int MatchTolerance { get; set; } = 40;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}