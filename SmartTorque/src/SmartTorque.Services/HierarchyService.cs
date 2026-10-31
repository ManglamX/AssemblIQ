using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

public record HierarchyItemDto(int Id, string Code, string Name, int? ParentId = null, string? Type = null);

public record CascadeOptionsDto(
    IReadOnlyList<HierarchyItemDto> Companies,
    IReadOnlyList<HierarchyItemDto> Plants,
    IReadOnlyList<HierarchyItemDto> Departments,
    IReadOnlyList<HierarchyItemDto> Units,
    IReadOnlyList<HierarchyItemDto> Stations);

public interface IHierarchyService
{
    Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<Plant>> GetPlantsAsync(int? companyId = null, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> GetDepartmentsAsync(int? plantId = null, CancellationToken ct = default);
    Task<IReadOnlyList<AssemblyLine>> GetProductionUnitsAsync(int? departmentId = null, ProductionUnitType? unitType = null, CancellationToken ct = default);
    Task<IReadOnlyList<WorkStation>> GetWorkStationsAsync(int? assemblyLineId = null, StationType? stationType = null, CancellationToken ct = default);
    Task<CascadeOptionsDto> GetCascadeOptionsAsync(CancellationToken ct = default);
    Task<(int? CompanyId, int? PlantId, int? DepartmentId)> ResolveOrCreateHierarchyAsync(
        string? companyName,
        string? plantName,
        string? departmentName,
        int? companyId = null,
        int? plantId = null,
        int? departmentId = null,
        CancellationToken ct = default);
}

public class HierarchyService : IHierarchyService
{
    private readonly AppDbContext _db;

    public HierarchyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Company>> GetCompaniesAsync(CancellationToken ct = default)
    {
        return await _db.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Plant>> GetPlantsAsync(int? companyId = null, CancellationToken ct = default)
    {
        var q = _db.Plants.Where(p => p.IsActive);
        if (companyId.HasValue)
            q = q.Where(p => p.CompanyId == companyId.Value);

        return await q.OrderBy(p => p.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Department>> GetDepartmentsAsync(int? plantId = null, CancellationToken ct = default)
    {
        var q = _db.Departments.Where(d => d.IsActive);
        if (plantId.HasValue)
            q = q.Where(d => d.PlantId == plantId.Value);

        return await q.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AssemblyLine>> GetProductionUnitsAsync(int? departmentId = null, ProductionUnitType? unitType = null, CancellationToken ct = default)
    {
        var q = _db.AssemblyLines.Where(a => a.IsActive);
        if (departmentId.HasValue)
            q = q.Where(a => a.DepartmentId == departmentId.Value);
        if (unitType.HasValue)
            q = q.Where(a => a.UnitType == unitType.Value);

        return await q.OrderBy(a => a.AssemblyName).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WorkStation>> GetWorkStationsAsync(int? assemblyLineId = null, StationType? stationType = null, CancellationToken ct = default)
    {
        var q = _db.WorkStations.Where(w => w.IsActive);
        if (assemblyLineId.HasValue)
            q = q.Where(w => w.AssemblyLineId == assemblyLineId.Value);
        if (stationType.HasValue)
            q = q.Where(w => w.StationType == stationType.Value);

        return await q.OrderBy(w => w.StationName).ToListAsync(ct);
    }

    public async Task<CascadeOptionsDto> GetCascadeOptionsAsync(CancellationToken ct = default)
    {
        var companies = await _db.Companies
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new HierarchyItemDto(c.CompanyId, c.Code, c.Name, null, null))
            .ToListAsync(ct);

        var plants = await _db.Plants
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new HierarchyItemDto(p.PlantId, p.Code, p.Name, p.CompanyId, null))
            .ToListAsync(ct);

        var departments = await _db.Departments
            .Where(d => d.IsActive)
            .OrderBy(d => d.Name)
            .Select(d => new HierarchyItemDto(d.DepartmentId, d.Code, d.Name, d.PlantId, null))
            .ToListAsync(ct);

        var units = await _db.AssemblyLines
            .Where(u => u.IsActive)
            .OrderBy(u => u.AssemblyName)
            .Select(u => new HierarchyItemDto(u.AssemblyLineId, u.AssemblyCode, $"{u.AssemblyName} ({(u.UnitType == ProductionUnitType.Cell ? "Cell" : "Line")})", u.DepartmentId, u.UnitType.ToString()))
            .ToListAsync(ct);

        var stations = await _db.WorkStations
            .Where(s => s.IsActive)
            .OrderBy(s => s.StationName)
            .Select(s => new HierarchyItemDto(s.WorkStationId, s.StationCode, $"{s.StationName} ({(s.StationType == StationType.Subcell ? "Subcell" : "Station")})", s.AssemblyLineId, s.StationType.ToString()))
            .ToListAsync(ct);

        return new CascadeOptionsDto(companies, plants, departments, units, stations);
    }

    public async Task<(int? CompanyId, int? PlantId, int? DepartmentId)> ResolveOrCreateHierarchyAsync(
        string? companyName,
        string? plantName,
        string? departmentName,
        int? companyId = null,
        int? plantId = null,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        // 1. Company
        var trimmedCompany = companyName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedCompany))
        {
            var existingComp = await _db.Companies
                .FirstOrDefaultAsync(co => co.Name.ToLower() == trimmedCompany.ToLower() || co.Code.ToLower() == trimmedCompany.ToLower(), ct);

            if (existingComp != null)
            {
                companyId = existingComp.CompanyId;
            }
            else
            {
                var code = GenerateCode(trimmedCompany);
                var newComp = new Company
                {
                    Name = trimmedCompany,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Companies.Add(newComp);
                await _db.SaveChangesAsync(ct);
                companyId = newComp.CompanyId;
            }
        }
        else if (companyId.HasValue)
        {
            var existing = await _db.Companies.FindAsync(new object[] { companyId.Value }, ct);
            if (existing == null) companyId = null;
        }

        // 2. Plant
        var trimmedPlant = plantName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedPlant))
        {
            var q = _db.Plants.AsQueryable();
            if (companyId.HasValue)
                q = q.Where(p => p.CompanyId == companyId.Value);

            var existingPlant = await q.FirstOrDefaultAsync(p => p.Name.ToLower() == trimmedPlant.ToLower() || p.Code.ToLower() == trimmedPlant.ToLower(), ct);
            if (existingPlant != null)
            {
                plantId = existingPlant.PlantId;
                if (!companyId.HasValue) companyId = existingPlant.CompanyId;
            }
            else if (companyId.HasValue)
            {
                var code = GenerateCode(trimmedPlant);
                var newPlant = new Plant
                {
                    CompanyId = companyId.Value,
                    Name = trimmedPlant,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Plants.Add(newPlant);
                await _db.SaveChangesAsync(ct);
                plantId = newPlant.PlantId;
            }
        }
        else if (plantId.HasValue)
        {
            var existing = await _db.Plants.FindAsync(new object[] { plantId.Value }, ct);
            if (existing == null) plantId = null;
            else if (!companyId.HasValue) companyId = existing.CompanyId;
        }

        // 3. Department
        var trimmedDept = departmentName?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedDept))
        {
            var q = _db.Departments.AsQueryable();
            if (plantId.HasValue)
                q = q.Where(d => d.PlantId == plantId.Value);

            var existingDept = await q.FirstOrDefaultAsync(d => d.Name.ToLower() == trimmedDept.ToLower() || d.Code.ToLower() == trimmedDept.ToLower(), ct);
            if (existingDept != null)
            {
                departmentId = existingDept.DepartmentId;
                if (!plantId.HasValue) plantId = existingDept.PlantId;
            }
            else if (plantId.HasValue)
            {
                var code = GenerateCode(trimmedDept);
                var newDept = new Department
                {
                    PlantId = plantId.Value,
                    Name = trimmedDept,
                    Code = code,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };
                _db.Departments.Add(newDept);
                await _db.SaveChangesAsync(ct);
                departmentId = newDept.DepartmentId;
            }
        }
        else if (departmentId.HasValue)
        {
            var existing = await _db.Departments.FindAsync(new object[] { departmentId.Value }, ct);
            if (existing == null) departmentId = null;
            else if (!plantId.HasValue) plantId = existing.PlantId;
        }

        return (companyId, plantId, departmentId);
    }

    private static string GenerateCode(string name)
    {
        var cleaned = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToUpperInvariant();
        if (cleaned.Length <= 8 && cleaned.Length >= 2) return cleaned;
        var words = name.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            var initials = new string(words.Select(w => char.ToUpperInvariant(w[0])).ToArray());
            return (initials + "-" + Random.Shared.Next(10, 99)).ToUpperInvariant();
        }
        var prefix = cleaned.Length > 0 ? cleaned[..Math.Min(cleaned.Length, 6)] : "DEPT";
        return (prefix + "-" + Random.Shared.Next(10, 99)).ToUpperInvariant();
    }
}
