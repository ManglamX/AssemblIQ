using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Encapsulates all business logic for building and managing Products
/// (a product is a collection of template components) — PRD-1 … PRD-7.
/// </summary>
public interface IProductService
{
    Task<List<Product>> ListAsync(ProductFilter filter, CancellationToken ct = default);

    /// <summary>Loads a product with its components, each component's template and that template's steps.</summary>
    Task<Product?> GetByIdAsync(int productId, CancellationToken ct = default);

    /// <summary>Loads a product by its unique ProductCode (Item Code) with components, templates and steps.</summary>
    Task<Product?> GetByCodeAsync(string productCode, CancellationToken ct = default);

    Task<bool> IsCodeUniqueAsync(string code, int? excludeProductId = null, CancellationToken ct = default);

    /// <summary>Latest, active template versions for the builder's template picker (Architecture §11).</summary>
    Task<List<Template>> GetLatestTemplatesAsync(CancellationToken ct = default);

    Task<Product> CreateAsync(SaveProductRequest request, string createdBy, CancellationToken ct = default);

    Task<Product> UpdateAsync(int productId, SaveProductRequest request, string editedBy, CancellationToken ct = default);

    /// <summary>Clones a product (and all its components) to create a variant with a new unique code.</summary>
    Task<Product> CloneAsync(int productId, string clonedBy, CancellationToken ct = default);

    Task SetActiveAsync(int productId, bool isActive, string changedBy, CancellationToken ct = default);

    /// <summary>Builds the old-vs-new step diff shown before a component is moved to the latest template version.</summary>
    Task<ComponentUpdatePreview> PreviewUpdateToLatestAsync(int productComponentId, CancellationToken ct = default);

    /// <summary>Repoints a component at the latest version of its template code (PRD, Architecture §11).</summary>
    Task<Product> ApplyUpdateToLatestAsync(int productComponentId, string changedBy, CancellationToken ct = default);
}

/// <summary>Filter parameters for the product list query.</summary>
public record ProductFilter(
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>Validated request passed from the controller to the service.</summary>
public record SaveProductRequest(
    string ProductCode,
    string ProductName,
    string? Description,
    string? ImagePath,
    List<ProductComponentRequest> Components);

public record ProductComponentRequest(
    int TemplateId,
    string ComponentName,
    int Quantity);

/// <summary>One row of the step diff between the old and the latest template version.</summary>
public record StepPreview(
    int SequenceNo,
    string Label,
    InstrumentType Instrument,
    decimal Target,
    decimal TolerancePlus,
    decimal ToleranceMinus,
    ToleranceMode ToleranceMode)
{
    public string Unit => Instrument == InstrumentType.Torque ? "N·m" : "mm";
}

public record ComponentUpdatePreview(
    int ProductComponentId,
    int ProductId,
    string ComponentName,
    int OldVersion,
    int NewVersion,
    bool IsAlreadyLatest,
    List<StepPreview> OldSteps,
    List<StepPreview> NewSteps,
    List<string> ChangedLabels);

public class ProductService : IProductService
{
    private readonly AppDbContext _db;

    public ProductService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Product>> ListAsync(ProductFilter filter, CancellationToken ct = default)
    {
        var query = _db.Products
            .Include(p => p.Components)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(p => p.ProductName.Contains(s) || p.ProductCode.Contains(s));
        }

        if (filter.IsActive.HasValue)
            query = query.Where(p => p.IsActive == filter.IsActive.Value);

        return await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<Product?> GetByIdAsync(int productId, CancellationToken ct = default)
        => await _db.Products
            .Include(p => p.Components.OrderBy(c => c.AssemblyOrder))
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ProductId == productId, ct);

    public async Task<Product?> GetByCodeAsync(string productCode, CancellationToken ct = default)
        => await _db.Products
            .Include(p => p.Components.OrderBy(c => c.AssemblyOrder))
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .AsSplitQuery()
            .FirstOrDefaultAsync(p => p.ProductCode == productCode, ct);

    public async Task<bool> IsCodeUniqueAsync(string code, int? excludeProductId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;
        var upper = code.Trim().ToUpperInvariant();
        var query = _db.Products.Where(p => p.ProductCode == upper);
        if (excludeProductId.HasValue)
            query = query.Where(p => p.ProductId != excludeProductId.Value);
        return !await query.AnyAsync(ct);
    }

    public async Task<List<Template>> GetLatestTemplatesAsync(CancellationToken ct = default)
        => await _db.Templates
            .Include(t => t.Steps)
            .Where(t => t.IsLatest)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<Product> CreateAsync(SaveProductRequest request, string createdBy, CancellationToken ct = default)
    {
        var components = ValidateComponents(request.Components);

        var product = new Product
        {
            ProductCode = request.ProductCode.Trim().ToUpperInvariant(),
            ProductName = request.ProductName.Trim(),
            Description = request.Description,
            ImagePath = request.ImagePath,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Components = MapComponents(components)
        };

        if (!await IsCodeUniqueAsync(product.ProductCode, null, ct))
            throw new InvalidOperationException($"A product with code '{product.ProductCode}' already exists.");

        _db.Products.Add(product);
        AddAudit(createdBy, product, "Created", new { product.ProductCode, product.ProductName, ComponentCount = product.Components.Count });

        await _db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product> UpdateAsync(int productId, SaveProductRequest request, string editedBy, CancellationToken ct = default)
    {
        var product = await _db.Products
            .Include(p => p.Components)
            .FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var components = ValidateComponents(request.Components);
        var newCode = request.ProductCode.Trim().ToUpperInvariant();

        if (!string.Equals(newCode, product.ProductCode, StringComparison.OrdinalIgnoreCase)
            && !await IsCodeUniqueAsync(newCode, productId, ct))
        {
            throw new InvalidOperationException($"A product with code '{newCode}' already exists.");
        }

        product.ProductCode = newCode;
        product.ProductName = request.ProductName.Trim();
        product.Description = request.Description;
        product.ImagePath = request.ImagePath;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = editedBy;

        // Replace the component set, preserving the submitted assembly order.
        _db.ProductComponents.RemoveRange(product.Components);
        _db.ProductComponents.AddRange(MapComponentsFor(product.ProductId, components));

        AddAudit(editedBy, product, "Updated", new { product.ProductCode, product.ProductName, ComponentCount = components.Count });

        await _db.SaveChangesAsync(ct);
        return product;
    }

    public async Task<Product> CloneAsync(int productId, string clonedBy, CancellationToken ct = default)
    {
        var source = await _db.Products
            .Include(p => p.Components)
            .FirstOrDefaultAsync(p => p.ProductId == productId, ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        var clone = new Product
        {
            ProductCode = await GenerateCopyCodeAsync(source.ProductCode, ct),
            ProductName = $"{source.ProductName} (Copy)",
            Description = source.Description,
            ImagePath = source.ImagePath,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = clonedBy,
            Components = source.Components
                .OrderBy(c => c.AssemblyOrder)
                .Select(c => new ProductComponent
                {
                    TemplateId = c.TemplateId,
                    ComponentName = c.ComponentName,
                    Quantity = c.Quantity,
                    AssemblyOrder = c.AssemblyOrder
                })
                .ToList()
        };

        _db.Products.Add(clone);
        AddAudit(clonedBy, clone, "Cloned", new { SourceProductId = source.ProductId, clone.ProductCode, ComponentCount = clone.Components.Count });

        await _db.SaveChangesAsync(ct);
        return clone;
    }

    public async Task SetActiveAsync(int productId, bool isActive, string changedBy, CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync([productId], ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        product.IsActive = isActive;
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = changedBy;

        AddAudit(changedBy, product, isActive ? "Activated" : "Deactivated", null);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<ComponentUpdatePreview> PreviewUpdateToLatestAsync(int productComponentId, CancellationToken ct = default)
    {
        var component = await _db.ProductComponents
            .Include(c => c.Template).ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(c => c.ProductComponentId == productComponentId, ct)
            ?? throw new InvalidOperationException($"Product component {productComponentId} not found.");

        var latest = await _db.Templates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.TemplateCode == component.Template.TemplateCode && t.IsLatest, ct);

        var oldSteps = MapStepPreviews(component.Template.Steps);
        var isAlreadyLatest = latest == null || latest.TemplateId == component.TemplateId;
        var newSteps = latest == null ? [] : MapStepPreviews(latest.Steps);

        var changed = oldSteps
            .Where(o => newSteps.Any(n => n.SequenceNo == o.SequenceNo &&
                (!string.Equals(n.Label, o.Label, StringComparison.Ordinal) ||
                 n.Instrument != o.Instrument ||
                 n.Target != o.Target ||
                 n.TolerancePlus != o.TolerancePlus ||
                 n.ToleranceMinus != o.ToleranceMinus ||
                 n.ToleranceMode != o.ToleranceMode)))
            .Select(o => o.Label)
            .ToList();

        return new ComponentUpdatePreview(
            component.ProductComponentId,
            component.ProductId,
            component.ComponentName,
            component.Template.Version,
            latest?.Version ?? component.Template.Version,
            isAlreadyLatest,
            oldSteps,
            newSteps,
            changed);
    }

    public async Task<Product> ApplyUpdateToLatestAsync(int productComponentId, string changedBy, CancellationToken ct = default)
    {
        var component = await _db.ProductComponents
            .Include(c => c.Template)
            .FirstOrDefaultAsync(c => c.ProductComponentId == productComponentId, ct)
            ?? throw new InvalidOperationException($"Product component {productComponentId} not found.");

        var latest = await _db.Templates
            .FirstOrDefaultAsync(t => t.TemplateCode == component.Template.TemplateCode && t.IsLatest, ct)
            ?? throw new InvalidOperationException($"No latest version found for template '{component.Template.TemplateCode}'.");

        if (latest.TemplateId == component.TemplateId)
            throw new InvalidOperationException($"Component '{component.ComponentName}' is already on the latest template version.");

        var oldVersion = component.Template.Version;
        component.TemplateId = latest.TemplateId;

        var product = await _db.Products.FirstAsync(p => p.ProductId == component.ProductId, ct);
        product.UpdatedAt = DateTime.UtcNow;
        product.UpdatedBy = changedBy;

        AddAudit(changedBy, product, "ComponentVersionUpdated",
            new { component.ComponentName, component.Template.TemplateCode, FromVersion = oldVersion, ToVersion = latest.Version });

        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(product.ProductId, ct) ?? product;
    }

    // -------------------------------------------------------
    private static List<ProductComponentRequest> ValidateComponents(List<ProductComponentRequest>? components)
    {
        if (components == null || components.Count == 0)
            throw new InvalidOperationException("A product must have at least one component.");

        if (components.Any(c => string.IsNullOrWhiteSpace(c.ComponentName)))
            throw new InvalidOperationException("Every component needs a name.");

        if (components.Any(c => c.Quantity < 1))
            throw new InvalidOperationException("Component quantity must be at least 1.");

        if (components.Any(c => c.TemplateId <= 0))
            throw new InvalidOperationException("Every component must reference a template.");

        return components;
    }

    private static List<ProductComponent> MapComponents(List<ProductComponentRequest> components)
        => components.Select((c, i) => new ProductComponent
        {
            TemplateId = c.TemplateId,
            ComponentName = c.ComponentName.Trim(),
            Quantity = c.Quantity,
            AssemblyOrder = i + 1
        }).ToList();

    private static List<ProductComponent> MapComponentsFor(int productId, List<ProductComponentRequest> components)
        => components.Select((c, i) => new ProductComponent
        {
            ProductId = productId,
            TemplateId = c.TemplateId,
            ComponentName = c.ComponentName.Trim(),
            Quantity = c.Quantity,
            AssemblyOrder = i + 1
        }).ToList();

    private static List<StepPreview> MapStepPreviews(IEnumerable<TemplateStep> steps)
        => steps.OrderBy(s => s.SequenceNo).Select(s => new StepPreview(
            s.SequenceNo, s.Label, s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)).ToList();

    private async Task<string> GenerateCopyCodeAsync(string baseCode, CancellationToken ct)
    {
        var candidate = $"{baseCode}-COPY";
        var n = 2;
        while (!await IsCodeUniqueAsync(candidate, null, ct))
        {
            candidate = $"{baseCode}-COPY{n++}";
        }
        return candidate;
    }

    private void AddAudit(string user, Product product, string action, object? changes)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = user,
            UserName = user,
            EntityName = nameof(Product),
            EntityId = product.ProductId == 0 ? product.ProductCode : product.ProductId.ToString(),
            Action = action,
            Changes = changes == null ? null : JsonSerializer.Serialize(changes)
        });
    }
}