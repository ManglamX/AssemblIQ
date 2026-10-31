namespace SmartTorque.Core.Entities;

/// <summary>
/// A sellable assembly (e.g. a 6-panel locker) built from one or more
/// <see cref="ProductComponent"/> rows, each referencing an exact template version.
/// </summary>
public class Product
{
    public int ProductId { get; set; }

    /// <summary>Short uppercase code, unique across products, e.g. "LKR-6P".</summary>
    public string ProductCode { get; set; } = string.Empty;

    public string ProductName { get; set; } = string.Empty;

    /// <summary>Department foreign key for strict organizational isolation.</summary>
    public int? DepartmentId { get; set; }

    public Department? Department { get; set; }

    public string? Description { get; set; }

    /// <summary>Relative path under wwwroot/uploads/products/.</summary>
    public string? ImagePath { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    // Navigation
    public ICollection<ProductComponent> Components { get; set; } = new List<ProductComponent>();

    public ICollection<ProductAssemblyMapping> AssemblyMappings { get; set; } = new List<ProductAssemblyMapping>();

    /// <summary>Ordered image gallery; row 0 is the primary image mirrored to <see cref="ImagePath"/>.</summary>
    public ICollection<ProductImage> Images { get; set; } = new List<ProductImage>();
}
