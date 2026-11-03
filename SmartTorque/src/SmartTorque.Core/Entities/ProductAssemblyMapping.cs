using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Connects a Product (or granular component/step package) to its designated Assembly Line or Cell.
/// Supports full product mapping, specific template mapping, or specific step mapping.
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

    /// <summary>Scope of allocation: FullProduct = 1, SelectedTemplates = 2, SelectedSteps = 3.</summary>
    public AllocationScopeType ScopeType { get; set; } = AllocationScopeType.FullProduct;

    /// <summary>Comma-separated TemplateIds assigned to this line mapping, or null for all templates.</summary>
    public string? AllowedTemplateIds { get; set; }

    /// <summary>Comma-separated TemplateStepIds assigned to this line mapping, or null for all steps.</summary>
    public string? AllowedStepIds { get; set; }

    /// <summary>Human-readable scope description, e.g. "Full Product" or "Templates: Main Frame" or "Steps 1–3".</summary>
    public string? ScopeSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedBy { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public string? UpdatedBy { get; set; }

    // Navigation
    public Product Product { get; set; } = null!;

    public AssemblyLine AssemblyLine { get; set; } = null!;
}