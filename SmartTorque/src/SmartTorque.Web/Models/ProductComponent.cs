using System;
using System.Collections.Generic;

namespace SmartTorque.Web.Models;

public partial class ProductComponent
{
    public int ProductComponentId { get; set; }

    public int ProductId { get; set; }

    public int TemplateId { get; set; }

    public string ComponentName { get; set; } = null!;

    public int Quantity { get; set; }

    public int AssemblyOrder { get; set; }

    public virtual Product Product { get; set; } = null!;

    public virtual Template Template { get; set; } = null!;
}