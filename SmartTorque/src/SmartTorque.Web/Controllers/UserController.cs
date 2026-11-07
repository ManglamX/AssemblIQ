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
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin && currentUser != null)
        {
            // Department Admin view:
            // Allowed to see and approve workers and admins requesting to join or belonging to their department
            // Includes all department instances matching by ID, name, code, or hierarchy node / child lines
            var (deptIds, nodeIds, lineIds) = await GetDeptAdminScopeIdsAsync(currentUser, currentUserCtx);
            var deptNameLower = (currentUserCtx.DepartmentName ?? string.Empty).Trim().ToLowerInvariant();

            query = query.Where(u => !u.IsMainAdmin && !u.IsPlantHead &&
                (
                    (u.DepartmentId.HasValue && deptIds.Contains(u.DepartmentId.Value)) ||
                    (u.HierarchyNodeId.HasValue && nodeIds.Contains(u.HierarchyNodeId.Value)) ||
                    (u.CurrentAssemblyLineId.HasValue && lineIds.Contains(u.CurrentAssemblyLineId.Value)) ||
                    (!string.IsNullOrEmpty(deptNameLower) && u.Department != null && u.Department.Name.ToLower() == deptNameLower)
                ));
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
                var selectedDept = await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == effectiveDeptId.Value);
                if (selectedDept != null)
                {
                    var matchingDeptIds = await _db.Departments
                        .Where(d => d.Name.ToLower() == selectedDept.Name.ToLower())
                        .Select(d => d.DepartmentId)
                        .ToListAsync();
                    var sName = selectedDept.Name.ToLower();
                    query = query.Where(u => (u.DepartmentId.HasValue && matchingDeptIds.Contains(u.DepartmentId.Value)) || (u.Department != null && u.Department.Name.ToLower() == sName));
                }
                else
                {
                    query = query.Where(u => u.DepartmentId == effectiveDeptId.Value);
                }
            }
        }
        else if (effectiveDeptId.HasValue)
        {
            var selectedDept = await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == effectiveDeptId.Value);
            if (selectedDept != null)
            {
                var matchingDeptIds = await _db.Departments
                    .Where(d => d.Name.ToLower() == selectedDept.Name.ToLower())
                    .Select(d => d.DepartmentId)
                    .ToListAsync();
                var sName = selectedDept.Name.ToLower();
                query = query.Where(u => (u.DepartmentId.HasValue && matchingDeptIds.Contains(u.DepartmentId.Value)) || (u.Department != null && u.Department.Name.ToLower() == sName));
            }
            else
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
                UserName = u.UserName ?? string.Empty,

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

                DepartmentName = u.Department?.Name ?? u.HierarchyNode?.Name,

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



        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);

        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (currentUser == null || !await IsUserInDeptAdminScopeAsync(currentUser, currentUserCtx, user))
            {
                return Forbid();
            }

            departmentId = currentUserCtx.DepartmentId;
            if (currentUser.PlantId.HasValue) user.PlantId = currentUser.PlantId;
            if (currentUser.CompanyId.HasValue) user.CompanyId = currentUser.CompanyId;
            if (currentUser.HierarchyNodeId.HasValue) user.HierarchyNodeId = currentUser.HierarchyNodeId;
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



        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);

        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (currentUser == null || !await IsUserInDeptAdminScopeAsync(currentUser, currentUserCtx, user))
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



        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);

        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (currentUser == null || !await IsUserInDeptAdminScopeAsync(currentUser, currentUserCtx, user))
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



        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);

        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (currentUser == null || !await IsUserInDeptAdminScopeAsync(currentUser, currentUserCtx, user))
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


    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/Delete")]
    public async Task<IActionResult> Delete(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var currentUser = User?.Identity?.IsAuthenticated == true ? await _userManager.GetUserAsync(User) : null;
        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        if (currentUserCtx != null && currentUserCtx.IsDeptAdmin)
        {
            if (currentUser == null || !await IsUserInDeptAdminScopeAsync(currentUser, currentUserCtx, user))
            {
                return Forbid();
            }
        }

        if (user.IsMainAdmin)
        {
            TempData["Error"] = "Cannot delete the Main Administrator.";
            return RedirectToAction(nameof(Index));
        }

        if (user.UserName == User?.Identity?.Name || user.Id == currentUserCtx?.UserId)
        {
            TempData["Error"] = "You cannot delete your own administrator account.";
            return RedirectToAction(nameof(Index));
        }

        // Check if user has historical audit sessions
        bool hasSessions = await _db.Sessions.AnyAsync(s => s.WorkerId == userId);
        if (hasSessions)
        {
            user.IsActive = false;
            user.CurrentAssemblyLineId = null;
            user.WorkStationId = null;
            user.IsLineSupervisor = false;
            user.UpdatedAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);

            TempData["Warning"] = $"User '{user.FullName}' ({user.Email}) has historical session audit records and cannot be permanently deleted. The user has been deactivated and unassigned instead.";
            return RedirectToAction(nameof(Index));
        }

        // Clean up references before hard delete
        var assignedJobs = await _db.Jobs.Where(j => j.AssignedWorkerId == userId || j.AssignedSupervisorId == userId).ToListAsync();
        foreach (var j in assignedJobs)
        {
            if (j.AssignedWorkerId == userId) j.AssignedWorkerId = null;
            if (j.AssignedSupervisorId == userId) j.AssignedSupervisorId = null;
        }

        var completedUnits = await _db.JobUnits.Where(u => u.CompletedByWorkerId == userId).ToListAsync();
        foreach (var u in completedUnits)
        {
            u.CompletedByWorkerId = null;
        }

        var assignedNodes = await _db.HierarchyNodes.Where(n => n.AdminUserId == userId).ToListAsync();
        foreach (var n in assignedNodes)
        {
            n.AdminUserId = null;
        }

        var grants = await _db.ApiKeyAccessGrants.Where(g => g.UserId == userId || g.DecidedByUserId == userId).ToListAsync();
        if (grants.Any())
        {
            _db.ApiKeyAccessGrants.RemoveRange(grants);
        }

        var nodeRequests = await _db.HierarchyNodeRequests.Where(r => r.RequestedByUserId == userId || r.ApproverUserId == userId).ToListAsync();
        if (nodeRequests.Any())
        {
            _db.HierarchyNodeRequests.RemoveRange(nodeRequests);
        }

        await _db.SaveChangesAsync();

        var delResult = await _userManager.DeleteAsync(user);
        if (delResult.Succeeded)
        {
            TempData["Success"] = $"User '{user.FullName}' ({user.Email}) was permanently deleted.";
        }
        else
        {
            TempData["Error"] = $"Failed to delete user: {string.Join(", ", delResult.Errors.Select(e => e.Description))}";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<(List<int> DeptIds, List<int> NodeIds, List<int> LineIds)> GetDeptAdminScopeIdsAsync(
        ApplicationUser adminUser,
        UserContextDto adminCtx,
        CancellationToken ct = default)
    {
        var deptIds = new List<int>();
        if (adminCtx.DepartmentId.HasValue) deptIds.Add(adminCtx.DepartmentId.Value);

        if (!string.IsNullOrWhiteSpace(adminCtx.DepartmentName))
        {
            var deptName = adminCtx.DepartmentName.Trim().ToLowerInvariant();
            var nameDeptIds = await _db.Departments
                .Where(d => d.Name.ToLower() == deptName || (d.Code != null && d.Code.ToLower() == deptName))
                .Select(d => d.DepartmentId)
                .ToListAsync(ct);
            deptIds.AddRange(nameDeptIds);
        }
        deptIds = deptIds.Distinct().ToList();

        var nodeIds = new List<int>();
        if (adminUser.HierarchyNodeId.HasValue)
        {
            nodeIds.Add(adminUser.HierarchyNodeId.Value);
            var adminNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeId == adminUser.HierarchyNodeId.Value, ct);
            if (adminNode != null && !string.IsNullOrEmpty(adminNode.Path))
            {
                var children = await _db.HierarchyNodes
                    .Where(n => n.Path.StartsWith(adminNode.Path))
                    .Select(n => n.NodeId)
                    .ToListAsync(ct);
                nodeIds.AddRange(children);
            }
        }

        if (!string.IsNullOrWhiteSpace(adminCtx.DepartmentName))
        {
            var deptName = adminCtx.DepartmentName.Trim().ToLowerInvariant();
            var matchingNodes = await _db.HierarchyNodes
                .Where(n => n.Name.ToLower() == deptName || n.Code.ToLower() == deptName)
                .ToListAsync(ct);
            foreach (var mn in matchingNodes)
            {
                nodeIds.Add(mn.NodeId);
                if (!string.IsNullOrEmpty(mn.Path))
                {
                    var ch = await _db.HierarchyNodes
                        .Where(n => n.Path.StartsWith(mn.Path))
                        .Select(n => n.NodeId)
                        .ToListAsync(ct);
                    nodeIds.AddRange(ch);
                }
            }
        }
        nodeIds = nodeIds.Distinct().ToList();

        var adminDeptName = (adminCtx.DepartmentName ?? string.Empty).Trim().ToLowerInvariant();
        var lineIds = await _db.AssemblyLines
            .Where(a => (a.DepartmentId.HasValue && deptIds.Contains(a.DepartmentId.Value)) ||
                        (!string.IsNullOrEmpty(adminDeptName) && a.Department.ToLower() == adminDeptName))
            .Select(a => a.AssemblyLineId)
            .ToListAsync(ct);

        return (deptIds, nodeIds, lineIds);
    }

    private async Task<bool> IsUserInDeptAdminScopeAsync(
        ApplicationUser adminUser,
        UserContextDto adminCtx,
        ApplicationUser targetUser,
        CancellationToken ct = default)
    {
        if (targetUser.IsMainAdmin || targetUser.IsPlantHead)
            return false;

        var (deptIds, nodeIds, lineIds) = await GetDeptAdminScopeIdsAsync(adminUser, adminCtx, ct);

        if (targetUser.DepartmentId.HasValue && deptIds.Contains(targetUser.DepartmentId.Value))
            return true;

        if (targetUser.HierarchyNodeId.HasValue && nodeIds.Contains(targetUser.HierarchyNodeId.Value))
            return true;

        if (targetUser.CurrentAssemblyLineId.HasValue && lineIds.Contains(targetUser.CurrentAssemblyLineId.Value))
            return true;

        if (!string.IsNullOrWhiteSpace(adminCtx.DepartmentName) && targetUser.DepartmentId.HasValue)
        {
            var targetDept = await _db.Departments.FirstOrDefaultAsync(d => d.DepartmentId == targetUser.DepartmentId.Value, ct);
            if (targetDept != null && string.Equals(targetDept.Name, adminCtx.DepartmentName, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}