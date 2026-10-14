using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// One measurement step inside a template (one bolt, one dimension, etc.).
/// </summary>
public class TemplateStep
{
    public int TemplateStepId { get; set; }

    public int TemplateId { get; set; }

    /// <summary>Display order; unique within a template.</summary>
    public int SequenceNo { get; set; }

    /// <summary>Torque or Vernier — determines unit and instrument handler.</summary>
    public InstrumentType Instrument { get; set; }

    /// <summary>e.g. "Bolt 1 – Top Left" or "Side A – Length"</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>Target measurement. Must be > 0. Stored as decimal for precision.</summary>
    public decimal Target { get; set; }

    /// <summary>Upper tolerance (must be ≥ 0).</summary>
    public decimal TolerancePlus { get; set; }

    /// <summary>Lower tolerance (must be ≥ 0).</summary>
    public decimal ToleranceMinus { get; set; }

    public ToleranceMode ToleranceMode { get; set; } = ToleranceMode.Absolute;

    /// <summary>Maximum retries before the step is forced-failed. Defaults to global setting.</summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>Marker X position as percentage (0–100) of image width. Null = not placed.</summary>
    public decimal? MarkerX { get; set; }

    /// <summary>Marker Y position as percentage (0–100) of image height. Null = not placed.</summary>
    public decimal? MarkerY { get; set; }

    // Navigation
    public Template Template { get; set; } = null!;
}