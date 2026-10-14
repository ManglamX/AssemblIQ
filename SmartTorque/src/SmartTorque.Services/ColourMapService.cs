using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Admin CRUD for the colour → priority mapping (PRD BM-3, Architecture §7.3).
/// The import pipeline reads <see cref="GetActiveAsync"/> to match detected fills.
/// </summary>
public interface IColourMapService
{
    Task<List<PriorityColourMap>> ListAsync(bool includeInactive = false, CancellationToken ct = default);

    Task<PriorityColourMap?> GetByIdAsync(int mapId, CancellationToken ct = default);

    /// <summary>Active shades ordered Black first — the set the parser matches against.</summary>
    Task<List<PriorityColourMap>> GetActiveAsync(CancellationToken ct = default);

    Task<PriorityColourMap> CreateAsync(ColourMapRequest request, string createdBy, CancellationToken ct = default);

    Task<PriorityColourMap> UpdateAsync(int mapId, ColourMapRequest request, string updatedBy, CancellationToken ct = default);

    /// <summary>Soft delete: a deactivated shade stays in history but is no longer matched.</summary>
    Task SetActiveAsync(int mapId, bool isActive, string changedBy, CancellationToken ct = default);
}

/// <summary>Validated input from the colour map screen.</summary>
public record ColourMapRequest(
    string ColourHex,
    PriorityLevel Priority,
    string Label,
    int MatchTolerance);

public class ColourMapService : IColourMapService
{
    private const int MaxRgbDistance = 441; // sqrt(3 * 255^2)

    private readonly AppDbContext _db;

    public ColourMapService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PriorityColourMap>> ListAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = _db.PriorityColourMap.AsQueryable();
        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        return await query
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Label)
            .ToListAsync(ct);
    }

    public Task<PriorityColourMap?> GetByIdAsync(int mapId, CancellationToken ct = default)
        => _db.PriorityColourMap.FirstOrDefaultAsync(m => m.MapId == mapId, ct);

    public async Task<List<PriorityColourMap>> GetActiveAsync(CancellationToken ct = default)
        => await _db.PriorityColourMap
            .Where(m => m.IsActive)
            .OrderBy(m => m.Priority)
            .ThenBy(m => m.Label)
            .ToListAsync(ct);

    public async Task<PriorityColourMap> CreateAsync(ColourMapRequest request, string createdBy, CancellationToken ct = default)
    {
        var (hex, priority, label, tolerance) = Validate(request);

        if (await _db.PriorityColourMap.AnyAsync(m => m.ColourHex == hex, ct))
            throw new InvalidOperationException($"A shade for {hex} already exists.");

        var entry = new PriorityColourMap
        {
            ColourHex = hex,
            Priority = priority,
            Label = label,
            MatchTolerance = tolerance,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.PriorityColourMap.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<PriorityColourMap> UpdateAsync(int mapId, ColourMapRequest request, string updatedBy, CancellationToken ct = default)
    {
        var entry = await _db.PriorityColourMap.FirstOrDefaultAsync(m => m.MapId == mapId, ct)
            ?? throw new InvalidOperationException($"Colour map entry {mapId} not found.");

        var (hex, priority, label, tolerance) = Validate(request);

        if (await _db.PriorityColourMap.AnyAsync(m => m.ColourHex == hex && m.MapId != mapId, ct))
            throw new InvalidOperationException($"A shade for {hex} already exists.");

        entry.ColourHex = hex;
        entry.Priority = priority;
        entry.Label = label;
        entry.MatchTolerance = tolerance;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = updatedBy;

        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task SetActiveAsync(int mapId, bool isActive, string changedBy, CancellationToken ct = default)
    {
        var entry = await _db.PriorityColourMap.FirstOrDefaultAsync(m => m.MapId == mapId, ct)
            ?? throw new InvalidOperationException($"Colour map entry {mapId} not found.");

        // Never allow the palette to lose every shade — the importer needs at least one.
        if (!isActive && !await _db.PriorityColourMap.AnyAsync(m => m.MapId != mapId && m.IsActive, ct))
            throw new InvalidOperationException("At least one active shade is required.");

        entry.IsActive = isActive;
        entry.UpdatedAt = DateTime.UtcNow;
        entry.UpdatedBy = changedBy;

        await _db.SaveChangesAsync(ct);
    }

    // -------------------------------------------------------
    private static (string Hex, PriorityLevel Priority, string Label, int Tolerance) Validate(ColourMapRequest request)
    {
        var hex = (request.ColourHex ?? string.Empty).Trim().ToUpperInvariant();

        if (!System.Text.RegularExpressions.Regex.IsMatch(hex, "^#[0-9A-F]{6}$"))
            throw new InvalidOperationException("Colour must be a hex value like #000000.");

        if (request.Priority < PriorityLevel.Black || request.Priority > PriorityLevel.White)
            throw new InvalidOperationException("Priority must be 1 (Black) to 5 (White).");

        var label = (request.Label ?? string.Empty).Trim();
        if (label.Length == 0)
            throw new InvalidOperationException("A label is required.");
        if (label.Length > 30)
            throw new InvalidOperationException("Label must be 30 characters or fewer.");

        if (request.MatchTolerance < 1 || request.MatchTolerance > MaxRgbDistance)
            throw new InvalidOperationException($"Match tolerance must be between 1 and {MaxRgbDistance}.");

        return (hex, request.Priority, label, request.MatchTolerance);
    }
}