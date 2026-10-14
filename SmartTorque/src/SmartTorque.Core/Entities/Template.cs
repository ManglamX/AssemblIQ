using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A reusable measurement template. One row = one (Code, Version) pair.
/// Only one version per Code may have IsLatest = true.
/// </summary>
public class Template
{
    public int TemplateId { get; set; }

    /// <summary>Short uppercase code, e.g. "BLT-01". Unique per version family.</summary>
    public string TemplateCode { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    /// <summary>True for exactly one version per TemplateCode.</summary>
    public bool IsLatest { get; set; } = true;

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Determines default instrument pre-fill in wizard; step instrument can override.</summary>
    public TemplateMode Mode { get; set; }

    /// <summary>Relative path under wwwroot/uploads/templates/.</summary>
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    // Navigation
    public ICollection<TemplateStep> Steps { get; set; } = new List<TemplateStep>();
}