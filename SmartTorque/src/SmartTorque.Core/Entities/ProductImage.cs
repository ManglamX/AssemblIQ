namespace SmartTorque.Core.Entities;

/// <summary>
/// One image in a product's gallery, ordered by <see cref="SortOrder"/>.
/// The first row is also mirrored to <see cref="Product.ImagePath"/> so the
/// existing single-image consumers (lists, detail page, sessions) keep working.
/// </summary>
public class ProductImage
{
    public int ProductImageId { get; set; }

    public int ProductId { get; set; }

    /// <summary>Relative path under wwwroot/uploads/products/.</summary>
    public string ImagePath { get; set; } = string.Empty;

    /// <summary>Display order within the gallery (0 = first = primary image).</summary>
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public Product Product { get; set; } = null!;
}