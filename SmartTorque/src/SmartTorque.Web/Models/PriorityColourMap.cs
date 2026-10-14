using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class PriorityColourMap
{
    public int MapId { get; set; }

    public string ColourHex { get; set; } = null!;

    public byte Priority { get; set; }

    public string Label { get; set; } = null!;

    public int MatchTolerance { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }
}