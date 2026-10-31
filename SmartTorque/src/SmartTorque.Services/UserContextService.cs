using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Data;

namespace SmartTorque.Services;

public class UserContextDto
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string RoleName { get; set; } = string.Empty;
    public bool IsMainAdmin { get; set; }
    public bool IsPlantHead { get; set; }
    public bool IsWorker { get; set; }

    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }

    public int? PlantId { get; set; }
    public string? PlantName { get; set; }

    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }

    public int? CurrentAssemblyLineId { get; set; }
    public string? AssemblyLineName { get; set; }

    public int? WorkStationId { get; set; }
    public string? WorkStationName { get; set; }

    public bool HasFullPlantAccess => IsMainAdmin || IsPlantHead;

    public bool CanAccessDepartment(int? targetDeptId)
    {
        if (HasFullPlantAccess) return true;
        if (!targetDeptId.HasValue && !DepartmentId.HasValue) return true;
        return DepartmentId == targetDeptId;
    }

    public int? GetEffectiveDepartmentId(int? filterDeptId = null)
    {
        if (HasFullPlantAccess)
        {
            return filterDeptId;
        }
        return DepartmentId;
    }
}

public interface IUserContextService
{
    Task<UserContextDto?> GetCurrentUserContextAsync(ClaimsPrincipal? principal, CancellationToken ct = default);
    Task<UserContextDto?> GetUserContextByIdAsync(string userId, CancellationToken ct = default);
}

public class UserContextService : IUserContextService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public UserContextService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<UserContextDto?> GetCurrentUserContextAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        if (principal == null || !principal.Identity?.IsAuthenticated == true)
            return null;

        var user = await _userManager.GetUserAsync(principal);
        if (user == null)
            return null;

        return await BuildDtoAsync(user, ct);
    }

    public async Task<UserContextDto?> GetUserContextByIdAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
            return null;

        return await BuildDtoAsync(user, ct);
    }

    private async Task<UserContextDto> BuildDtoAsync(ApplicationUser user, CancellationToken ct)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var primaryRole = roles.FirstOrDefault() ?? user.RequestedRole ?? "Worker";

        string? companyName = null;
        if (user.CompanyId.HasValue)
        {
            companyName = await _db.Companies
                .Where(c => c.CompanyId == user.CompanyId.Value)
                .Select(c => c.Name)
                .FirstOrDefaultAsync(ct);
        }

        string? plantName = null;
        if (user.PlantId.HasValue)
        {
            plantName = await _db.Plants
                .Where(p => p.PlantId == user.PlantId.Value)
                .Select(p => p.Name)
                .FirstOrDefaultAsync(ct);
        }

        string? deptName = null;
        if (user.DepartmentId.HasValue)
        {
            deptName = await _db.Departments
                .Where(d => d.DepartmentId == user.DepartmentId.Value)
                .Select(d => d.Name)
                .FirstOrDefaultAsync(ct);
        }

        string? lineName = null;
        if (user.CurrentAssemblyLineId.HasValue)
        {
            lineName = await _db.AssemblyLines
                .Where(a => a.AssemblyLineId == user.CurrentAssemblyLineId.Value)
                .Select(a => a.AssemblyName)
                .FirstOrDefaultAsync(ct);
        }

        string? stationName = null;
        if (user.WorkStationId.HasValue)
        {
            stationName = await _db.WorkStations
                .Where(w => w.WorkStationId == user.WorkStationId.Value)
                .Select(w => w.StationName)
                .FirstOrDefaultAsync(ct);
        }

        return new UserContextDto
        {
            UserId = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            RoleName = primaryRole,
            IsMainAdmin = user.IsMainAdmin,
            IsPlantHead = user.IsPlantHead,
            IsWorker = primaryRole == "Worker",
            CompanyId = user.CompanyId,
            CompanyName = companyName,
            PlantId = user.PlantId,
            PlantName = plantName,
            DepartmentId = user.DepartmentId,
            DepartmentName = deptName,
            CurrentAssemblyLineId = user.CurrentAssemblyLineId,
            AssemblyLineName = lineName,
            WorkStationId = user.WorkStationId,
            WorkStationName = stationName
        };
    }
}
