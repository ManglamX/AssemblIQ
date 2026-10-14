using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Encapsulates all business logic for Template CRUD and versioning (TPL-1 … TPL-10).
/// </summary>
public interface ITemplateService
{
    Task<List<Template>> ListAsync(TemplateFilter filter, CancellationToken ct = default);
    Task<Template?> GetByIdAsync(int templateId, CancellationToken ct = default);
    Task<Template?> GetLatestByCodeAsync(string code, CancellationToken ct = default);
    Task<List<Template>> GetAllVersionsByCodeAsync(string code, CancellationToken ct = default);

    /// <summary>Creates a brand-new template (version 1).</summary>
    Task<Template> CreateAsync(CreateTemplateRequest request, string createdBy, CancellationToken ct = default);

    /// <summary>
    /// Edits a template.
    /// - If template has ever been used by a session: creates a new version (TPL-8).
    /// - Otherwise: edits in place.
    /// Returns the saved template (may have a new TemplateId if versioned).
    /// </summary>
    Task<Template> EditAsync(int templateId, CreateTemplateRequest request, string editedBy, CancellationToken ct = default);

    Task SetActiveAsync(int templateId, bool isActive, string changedBy, CancellationToken ct = default);

    /// <summary>Returns true if this template has been referenced by any session.</summary>
    Task<bool> IsUsedBySessionAsync(int templateId, CancellationToken ct = default);

    Task<bool> IsCodeUniqueAsync(string code, int? excludeTemplateId = null, CancellationToken ct = default);
}

/// <summary>Filter parameters for template list query.</summary>
public record TemplateFilter(
    string? Search = null,
    Core.Enums.TemplateMode? Mode = null,
    Core.Enums.InstrumentType? Instrument = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25);

/// <summary>Validated request object passed from controller to service.</summary>
public record CreateTemplateRequest(
    string TemplateCode,
    string Name,
    string? Description,
    Core.Enums.TemplateMode Mode,
    string? ImagePath,
    List<StepRequest> Steps);

public record StepRequest(
    int SequenceNo,
    Core.Enums.InstrumentType Instrument,
    string Label,
    decimal Target,
    decimal TolerancePlus,
    decimal ToleranceMinus,
    Core.Enums.ToleranceMode ToleranceMode,
    int MaxRetries,
    decimal? MarkerX,
    decimal? MarkerY);

public class TemplateService : ITemplateService
{
    private readonly AppDbContext _db;
    private readonly IImageUploadService _images;

    public TemplateService(AppDbContext db, IImageUploadService images)
    {
        _db = db;
        _images = images;
    }

    public async Task<List<Template>> ListAsync(TemplateFilter filter, CancellationToken ct = default)
    {
        var query = _db.Templates
            .Include(t => t.Steps)
            .Where(t => t.IsLatest)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            query = query.Where(t =>
                t.Name.Contains(s) || t.TemplateCode.Contains(s));
        }

        if (filter.Mode.HasValue)
            query = query.Where(t => t.Mode == filter.Mode.Value);

        if (filter.IsActive.HasValue)
            query = query.Where(t => t.IsActive == filter.IsActive.Value);

        if (filter.Instrument.HasValue)
            query = query.Where(t =>
                t.Steps.Any(s => s.Instrument == filter.Instrument.Value));

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(ct);
    }

    public async Task<Template?> GetByIdAsync(int templateId, CancellationToken ct = default)
        => await _db.Templates.Include(t => t.Steps.OrderBy(s => s.SequenceNo))
               .FirstOrDefaultAsync(t => t.TemplateId == templateId, ct);

    public async Task<Template?> GetLatestByCodeAsync(string code, CancellationToken ct = default)
        => await _db.Templates.Include(t => t.Steps.OrderBy(s => s.SequenceNo))
               .FirstOrDefaultAsync(t => t.TemplateCode == code && t.IsLatest, ct);

    public async Task<List<Template>> GetAllVersionsByCodeAsync(string code, CancellationToken ct = default)
        => await _db.Templates
               .Where(t => t.TemplateCode == code)
               .OrderByDescending(t => t.Version)
               .ToListAsync(ct);

    public async Task<Template> CreateAsync(CreateTemplateRequest request, string createdBy, CancellationToken ct = default)
    {
        var template = new Template
        {
            TemplateCode = request.TemplateCode.ToUpperInvariant(),
            Version = 1,
            IsLatest = true,
            Name = request.Name,
            Description = request.Description,
            Mode = request.Mode,
            ImagePath = request.ImagePath,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            Steps = MapSteps(request.Steps)
        };

        _db.Templates.Add(template);

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = createdBy,
            UserName = createdBy,
            EntityName = nameof(Template),
            EntityId = template.TemplateCode,
            Action = "Created",
            Changes = JsonSerializer.Serialize(new { template.TemplateCode, template.Version, template.Name, StepCount = template.Steps.Count })
        });

        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<Template> EditAsync(
        int templateId,
        CreateTemplateRequest request,
        string editedBy,
        CancellationToken ct = default)
    {
        var existing = await _db.Templates
            .Include(t => t.Steps)
            .FirstOrDefaultAsync(t => t.TemplateId == templateId, ct)
            ?? throw new InvalidOperationException($"Template {templateId} not found.");

        var isUsed = await IsUsedBySessionAsync(templateId, ct);

        if (isUsed)
        {
            // TPL-8: create a new version; old version stays
            var nextVersion = await _db.Templates
                .Where(t => t.TemplateCode == existing.TemplateCode)
                .MaxAsync(t => t.Version, ct) + 1;

            // Unmark old latest
            existing.IsLatest = false;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = editedBy;

            var newTemplate = new Template
            {
                TemplateCode = existing.TemplateCode,
                Version = nextVersion,
                IsLatest = true,
                Name = request.Name,
                Description = request.Description,
                Mode = request.Mode,
                ImagePath = request.ImagePath,
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = editedBy,
                Steps = MapSteps(request.Steps)
            };

            _db.Templates.Add(newTemplate);

            _db.AuditLogs.Add(new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                UserId = editedBy,
                UserName = editedBy,
                EntityName = nameof(Template),
                EntityId = newTemplate.TemplateCode,
                Action = "VersionCreated",
                Changes = JsonSerializer.Serialize(new { newTemplate.TemplateCode, newTemplate.Version, PreviousVersion = existing.Version })
            });

            await _db.SaveChangesAsync(ct);
            return newTemplate;
        }
        else
        {
            // Edit in place
            existing.Name = request.Name;
            existing.Description = request.Description;
            existing.Mode = request.Mode;
            existing.ImagePath = request.ImagePath;
            existing.UpdatedAt = DateTime.UtcNow;
            existing.UpdatedBy = editedBy;

            // Replace steps
            _db.TemplateSteps.RemoveRange(existing.Steps);
            existing.Steps = MapSteps(request.Steps);

            _db.AuditLogs.Add(new AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                UserId = editedBy,
                UserName = editedBy,
                EntityName = nameof(Template),
                EntityId = existing.TemplateId.ToString(),
                Action = "Updated",
                Changes = JsonSerializer.Serialize(new { existing.TemplateCode, existing.Version, existing.Name, StepCount = existing.Steps.Count })
            });

            await _db.SaveChangesAsync(ct);
            return existing;
        }
    }

    public async Task SetActiveAsync(int templateId, bool isActive, string changedBy, CancellationToken ct = default)
    {
        var template = await _db.Templates.FindAsync([templateId], ct)
            ?? throw new InvalidOperationException($"Template {templateId} not found.");
        template.IsActive = isActive;
        template.UpdatedAt = DateTime.UtcNow;
        template.UpdatedBy = changedBy;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Template),
            EntityId = template.TemplateId.ToString(),
            Action = isActive ? "Activated" : "Deactivated"
        });

        await _db.SaveChangesAsync(ct);
    }

    public virtual async Task<bool> IsUsedBySessionAsync(int templateId, CancellationToken ct = default)
    {
        // Phase 5 will add Sessions; for now returns false so new templates edit in-place.
        // When Sessions table is added, replace with: _db.Sessions.AnyAsync(s => s.TemplateId == templateId)
        await Task.CompletedTask;
        return false;
    }

    public async Task<bool> IsCodeUniqueAsync(string code, int? excludeTemplateId = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code)) return true;
        var upper = code.Trim().ToUpperInvariant();
        var query = _db.Templates.Where(t => t.TemplateCode == upper && t.IsLatest);
        if (excludeTemplateId.HasValue)
            query = query.Where(t => t.TemplateId != excludeTemplateId.Value);
        return !await query.AnyAsync(ct);
    }

    // -------------------------------------------------------
    private static List<TemplateStep> MapSteps(List<StepRequest> requests) =>
        requests.Select(r => new TemplateStep
        {
            SequenceNo = r.SequenceNo,
            Instrument = r.Instrument,
            Label = r.Label,
            Target = r.Target,
            TolerancePlus = r.TolerancePlus,
            ToleranceMinus = r.ToleranceMinus,
            ToleranceMode = r.ToleranceMode,
            MaxRetries = r.MaxRetries,
            MarkerX = r.MarkerX,
            MarkerY = r.MarkerY
        }).ToList();
}