namespace SmartTorque.Core.Entities;

/// <summary>
/// One component of a product. Points at the exact <see cref="Template"/> version used,
/// so historic products keep the design they were built with (Architecture §11).
/// </summary>
public class ProductComponent
{
    public int ProductComponentId { get; set; }

    public int ProductId { get; set; }

    /// <summary>FK to the exact template <b>version</b> (not just the code).</summary>
    public int TemplateId { get; set; }

    /// <summary>Human name for this use of the template, e.g. "Left Panel".</summary>
    public string ComponentName { get; set; } = string.Empty;

    /// <summary>How many of this component one product uses. Must be ≥ 1.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Order of assembly within the product.</summary>
    public int AssemblyOrder { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;

    public Template Template { get; set; } = null!;
}