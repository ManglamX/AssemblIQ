using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.User;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly IUserContextService _userContext;
    private readonly IHierarchyService _hierarchy;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        ILogger<UserController> logger,
        IUserContextService? userContext = null,
        IHierarchyService? hierarchy = null)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
        _userContext = userContext ?? new UserContextService(db, userManager);
        _hierarchy = hierarchy ?? new HierarchyService(db);
    }

    [HttpGet]
    [Route("User")]
    [Route("Admin/Users")]
    public async Task<IActionResult> Index(string? filter = null, int? departmentId = null)
    {
        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

        // If not plant head/main admin, lock strictly to the user's department (zero cross-seeing)
        int? effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(departmentId);

        IQueryable<ApplicationUser> query = _userManager.Users
            .Include(u => u.Department)
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .Include(u => u.HierarchyNode)
                .ThenInclude(n => n!.ParentNode);

        // Scoping:
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin && effectiveDeptId.HasValue)
        {
            // Department Admin view:
            // STRICTLY restricted to users of his department:
            // - Workers in his department
            // - Supervisors in his department
            // - Colleague Dept Admins in his department
            // HIGHER LEVEL USERS (IsMainAdmin, IsPlantHead) AND other departments MUST NEVER BE VISIBLE
            query = query.Where(u => u.DepartmentId == effectiveDeptId.Value && !u.IsMainAdmin && !u.IsPlantHead);
        }
        else if (currentUser != null && !currentUser.IsMainAdmin)
        {
            var managedNodeIds = await _db.HierarchyNodes
                .Where(n => n.AdminUserId == currentUser.Id && n.IsActive)
                .Select(n => n.NodeId)
                .ToListAsync();

            if (currentUser.PlantId.HasValue)
            {
                var plantNodes = await _db.HierarchyNodes
                    .Where(n => n.NodeType == HierarchyNodeType.Plant && n.IsActive && 
                        _db.Plants.Any(p => p.PlantId == currentUser.PlantId && p.Name == n.Name))
                    .Select(n => n.NodeId)
                    .ToListAsync();
                managedNodeIds.AddRange(plantNodes);
            }

            managedNodeIds = managedNodeIds.Distinct().ToList();

            // Find immediate child nodes under managed parent nodes
            var childNodeIds = await _db.HierarchyNodes
                .Where(n => n.ParentNodeId.HasValue && managedNodeIds.Contains(n.ParentNodeId.Value))
                .Select(n => n.NodeId)
                .ToListAsync();

            var allowedNodeIds = managedNodeIds.Concat(childNodeIds).Distinct().ToHashSet();

            query = query.Where(u =>
                (u.HierarchyNodeId.HasValue && allowedNodeIds.Contains(u.HierarchyNodeId.Value)) ||
                (u.HierarchyNode != null && u.HierarchyNode.ParentNodeId.HasValue && managedNodeIds.Contains(u.HierarchyNode.ParentNodeId.Value)) ||
                (currentUser.PlantId.HasValue && u.PlantId == currentUser.PlantId.Value));

            if (effectiveDeptId.HasValue)
            {
                query = query.Where(u => u.DepartmentId == effectiveDeptId.Value);
            }
        }

        var users = await query.OrderByDescending(u => u.CreatedAt).ToListAsync();

        var cards = new List<UserCardViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var currentRole = roles.FirstOrDefault() ?? u.RequestedRole ?? "Worker";

            cards.Add(new UserCardViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                EmployeeNo = u.EmployeeNo,
                RegistrationStatus = u.RegistrationStatus,
                RequestedRole = u.RequestedRole ?? currentRole,
                CurrentRole = currentRole,
                IsActive = u.IsActive,
                IsMainAdmin = u.IsMainAdmin,
                IsPlantHead = u.IsPlantHead,
                IsLineSupervisor = u.IsLineSupervisor,
                CreatedAt = u.CreatedAt,
                ReviewedAt = u.ReviewedAt,
                ReviewedBy = u.ReviewedBy,
                RejectionReason = u.RejectionReason,
                CompanyId = u.CompanyId,
                PlantId = u.PlantId,
                DepartmentId = u.DepartmentId,
                DepartmentName = u.Department?.Name,
                CurrentAssemblyLineId = u.CurrentAssemblyLineId,
                AssemblyLineName = u.CurrentAssemblyLine?.AssemblyName,
                WorkStationId = u.WorkStationId,
                WorkStationName = u.WorkStation?.StationName
            });
        }

        var pendingCount = cards.Count(c => c.RegistrationStatus == RegistrationStatus.Pending);
        var workersCount = cards.Count(c => c.CurrentRole == "Worker" && c.RegistrationStatus == RegistrationStatus.Approved);
        var adminsCount = cards.Count(c => c.CurrentRole == "Admin" && c.RegistrationStatus == RegistrationStatus.Approved);
        var rejectedCount = cards.Count(c => c.RegistrationStatus == RegistrationStatus.Rejected);

        // Default to pending if any pending exist, otherwise all
        var activeFilter = filter?.ToLowerInvariant() ?? (pendingCount > 0 ? "pending" : "all");

        var filtered = activeFilter switch
        {
            "pending" => cards.Where(c => c.RegistrationStatus == RegistrationStatus.Pending).ToList(),
            "workers" => cards.Where(c => c.CurrentRole == "Worker" && c.RegistrationStatus == RegistrationStatus.Approved).ToList(),
            "admins" => cards.Where(c => c.CurrentRole == "Admin" && c.RegistrationStatus == RegistrationStatus.Approved).ToList(),
            "rejected" => cards.Where(c => c.RegistrationStatus == RegistrationStatus.Rejected).ToList(),
            _ => cards
        };

        var depts = await _db.Departments.Where(d => d.IsActive).OrderBy(d => d.Name)
            .Select(d => new HierarchyItemDto(d.DepartmentId, d.Code, d.Name, d.PlantId, null))
            .ToListAsync();

        var vm = new UserListViewModel
        {
            ActiveFilter = activeFilter,
            PendingCount = pendingCount,
            WorkersCount = workersCount,
            AdminsCount = adminsCount,
            RejectedCount = rejectedCount,
            TotalCount = cards.Count,
            Users = filtered,
            SelectedDepartmentId = isPlantHead ? departmentId : effectiveDeptId,
            IsPlantHead = isPlantHead,
            CurrentUserDepartmentName = currentUserCtx?.DepartmentName,
            AvailableDepartments = depts
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/Approve")]
    public async Task<IActionResult> Approve(
        string userId,
        string? assignedRole = null,
        int? departmentId = null,
        int? assemblyLineId = null,
        int? workStationId = null)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if ((user.DepartmentId.HasValue && user.DepartmentId != currentUserCtx.DepartmentId) || user.IsMainAdmin || user.IsPlantHead)
            {
                return Forbid();
            }
            departmentId = currentUserCtx.DepartmentId;
        }

        var role = !string.IsNullOrWhiteSpace(assignedRole) ? assignedRole.Trim() : (user.RequestedRole ?? "Worker");
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin && (role == "PlantHead" || role == "MainAdmin"))
        {
            return Forbid();
        }

        user.RegistrationStatus = RegistrationStatus.Approved;
        user.IsActive = true;
        user.RejectionReason = null;
        user.ReviewedAt = DateTime.UtcNow;
        user.ReviewedBy = User?.Identity?.Name ?? "Admin";
        user.UpdatedAt = DateTime.UtcNow;

        if (departmentId.HasValue) user.DepartmentId = departmentId.Value;
        if (assemblyLineId.HasValue) user.CurrentAssemblyLineId = assemblyLineId.Value;
        if (workStationId.HasValue) user.WorkStationId = workStationId.Value;

        await _userManager.UpdateAsync(user);

        // Update role
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
            await _userManager.AddToRoleAsync(user, role);
        }

        _logger.LogInformation("User {Email} approved by {Admin} as {Role}", user.Email, User?.Identity?.Name, role);
        if (TempData != null)
        {
            TempData["Success"] = $"User {user.FullName} ({user.Email}) has been approved as {role}.";
        }

        return RedirectToAction(nameof(Index), new { filter = "pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/Reject")]
    public async Task<IActionResult> Reject(string userId, string? reason)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if ((user.DepartmentId.HasValue && user.DepartmentId != currentUserCtx.DepartmentId) || user.IsMainAdmin || user.IsPlantHead)
            {
                return Forbid();
            }
        }

        user.RegistrationStatus = RegistrationStatus.Rejected;
        user.IsActive = false;
        user.RejectionReason = !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : "Access request was rejected by an administrator.";
        user.ReviewedAt = DateTime.UtcNow;
        user.ReviewedBy = User?.Identity?.Name ?? "Admin";
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Email} rejected by {Admin}. Reason: {Reason}", user.Email, User?.Identity?.Name, user.RejectionReason);
        if (TempData != null)
        {
            TempData["Warning"] = $"User {user.FullName} ({user.Email}) registration has been rejected.";
        }

        return RedirectToAction(nameof(Index), new { filter = "pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/ToggleActive")]
    public async Task<IActionResult> ToggleActive(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if ((user.DepartmentId.HasValue && user.DepartmentId != currentUserCtx.DepartmentId) || user.IsMainAdmin || user.IsPlantHead)
            {
                return Forbid();
            }
        }

        // Prevent admin from deactivating themselves
        if (user.UserName == User?.Identity?.Name)
        {
            if (TempData != null)
            {
                TempData["Error"] = "You cannot deactivate your own administrator account.";
            }
            return RedirectToAction(nameof(Index), new { filter = "all" });
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (TempData != null)
        {
            TempData["Success"] = $"User {user.FullName} is now {(user.IsActive ? "Active" : "Deactivated")}.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/ToggleLineSupervisor")]
    public async Task<IActionResult> ToggleLineSupervisor(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (user.DepartmentId != currentUserCtx.DepartmentId || user.IsMainAdmin || user.IsPlantHead)
            {
                return Forbid();
            }
        }

        user.IsLineSupervisor = !user.IsLineSupervisor;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (TempData != null)
        {
            TempData["Success"] = user.IsLineSupervisor
                ? $"{user.FullName} is now designated as Line Supervisor for job dispatch."
                : $"{user.FullName}'s line supervisor designation has been revoked.";
        }

        return RedirectToAction(nameof(Index), new { filter = "workers" });
    }
}
