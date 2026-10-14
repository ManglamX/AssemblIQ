using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class Template
{
    public int TemplateId { get; set; }

    public string TemplateCode { get; set; } = null!;

    public int Version { get; set; }

    public bool IsLatest { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public byte Mode { get; set; }

    public string? ImagePath { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    public virtual ICollection<ProductComponent> ProductComponents { get; set; } = new List<ProductComponent>();

    public virtual ICollection<TemplateStep> TemplateSteps { get; set; } = new List<TemplateStep>();
}