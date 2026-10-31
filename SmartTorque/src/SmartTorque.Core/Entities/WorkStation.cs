using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Specific physical station (for Lines) or subcell (for Cells) where tasks/operations are executed.
/// </summary>
public class WorkStation
{
    public int WorkStationId { get; set; }

    public int AssemblyLineId { get; set; }

    public AssemblyLine AssemblyLine { get; set; } = null!;

    /// <summary>Station for assembly lines, Subcell for manufacturing cells.</summary>
    public StationType StationType { get; set; } = StationType.Station;

    /// <summary>Station identifier (e.g. "ST-01", "SUBCELL-A").</summary>
    public string StationCode { get; set; } = string.Empty;

    /// <summary>Display name (e.g. "Front Panel Assembly Station", "Spot Welding Subcell").</summary>
    public string StationName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public ICollection<ApplicationUser> AssignedWorkers { get; set; } = new List<ApplicationUser>();
}
