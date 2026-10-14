using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class TemplateStep
{
    public int TemplateStepId { get; set; }

    public int TemplateId { get; set; }

    public int SequenceNo { get; set; }

    public byte Instrument { get; set; }

    public string Label { get; set; } = null!;

    public decimal Target { get; set; }

    public decimal TolerancePlus { get; set; }

    public decimal ToleranceMinus { get; set; }

    public byte ToleranceMode { get; set; }

    public int MaxRetries { get; set; }

    public decimal? MarkerX { get; set; }

    public decimal? MarkerY { get; set; }

    public virtual Template Template { get; set; } = null!;
}