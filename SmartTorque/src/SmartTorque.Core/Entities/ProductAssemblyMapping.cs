namespace SmartTorque.Core.Entities;

/// <summary>
/// Connects a Product to its designated Assembly Line.
/// Supports single designated line and explicit multi-line routing.
/// </summary>
public class ProductAssemblyMapping
{
    public int ProductAssemblyMappingId { get; set; }

    public int ProductId { get; set; }

    public int AssemblyLineId { get; set; }

    /// <summary>
    /// Indicates whether this is the primary / designated assembly line for this product.
    /// By default true if the production process requires a fixed line.
    /// </summary>
    public bool IsDefault { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;

    public AssemblyLine AssemblyLine { get; set; } = null!;
}
