using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Account;



namespace SmartTorque.Web.Controllers;



/// <summary>

/// Handles authentication, registration, access status, and logout.

/// </summary>

[AllowAnonymous]

public class AccountController : Controller

{

    private readonly SignInManager<ApplicationUser> _signInManager;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly RoleManager<IdentityRole> _roleManager;

    private readonly IHierarchyService _hierarchy;

    private readonly AppDbContext _db;

    private readonly ILogger<AccountController> _logger;



    public AccountController(

        SignInManager<ApplicationUser> signInManager,

        UserManager<ApplicationUser> userManager,

        RoleManager<IdentityRole> roleManager,

        IHierarchyService hierarchy,

        AppDbContext db,

        ILogger<AccountController> logger)

    {

        _signInManager = signInManager;

        _userManager = userManager;

        _roleManager = roleManager;

        _hierarchy = hierarchy;

        _db = db;

        _logger = logger;

    }



    // GET /Account/Login

    [HttpGet]

    public IActionResult Login(string? returnUrl = null)

    {

        if (_signInManager.IsSignedIn(User))

            return RedirectToRoleHome();



        ViewData["ReturnUrl"] = returnUrl;

        return View();

    }



    // POST /Account/Login

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)

    {

        ViewData["ReturnUrl"] = returnUrl;



        if (!ModelState.IsValid)

            return View(model);



        var user = await _userManager.FindByEmailAsync(model.Email);

        if (user == null)

        {

            ModelState.AddModelError(string.Empty, "Invalid email or password.");

            return View(model);

        }



        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);

        if (!passwordValid)

        {

            // Triggers lockout count if configured

            await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: true);

            ModelState.AddModelError(string.Empty, "Invalid email or password.");

            return View(model);

        }



        // Check registration approval status

        if (user.RegistrationStatus == RegistrationStatus.Pending)

        {

            return RedirectToAction(nameof(AccessStatus), new { email = user.Email });

        }



        if (user.RegistrationStatus == RegistrationStatus.Rejected)

        {

            return RedirectToAction(nameof(AccessStatus), new { email = user.Email });

        }



        if (!user.IsActive)

        {

            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact your system administrator.");

            return View(model);

        }



        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("User {Email} logged in successfully", model.Email);

        // Always start with blank hardware tool on fresh login until explicitly connected
        SmartTorque.Web.Areas.Worker.Controllers.JobBoardController.ClearWorkerStationTool(user.Id);

        var roles = await _userManager.GetRolesAsync(user);
        bool isAdmin = roles.Contains("Admin") || string.Equals(user.RequestedRole, "Admin", StringComparison.OrdinalIgnoreCase);
        bool isWorker = roles.Contains("Worker") || string.Equals(user.RequestedRole, "Worker", StringComparison.OrdinalIgnoreCase);

        // Sanitize returnUrl according to user role to prevent cross-portal redirection on first login
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/" &&
            !returnUrl.Equals("/Home", StringComparison.OrdinalIgnoreCase) &&
            !returnUrl.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase))
        {
            if (isAdmin)
            {
                // Admin must NEVER be dumped into worker job board due to a leftover worker returnUrl
                if (!returnUrl.StartsWith("/Worker", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }
            }
            else if (isWorker)
            {
                // Worker must NEVER be routed into admin/management areas due to a leftover admin returnUrl
                if (!returnUrl.StartsWith("/Admin", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Hierarchy", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Product", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Device", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Settings", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Report", StringComparison.OrdinalIgnoreCase) &&
                    !returnUrl.StartsWith("/Users", StringComparison.OrdinalIgnoreCase))
                {
                    return Redirect(returnUrl);
                }
            }
            else
            {
                return Redirect(returnUrl);
            }
        }

        if (isAdmin)
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

        if (isWorker)
            return RedirectToAction("Index", "JobBoard", new { area = "Worker" });

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

    }



    // GET /Account/Register

    [HttpGet]

    public IActionResult Register()

    {

        if (_signInManager.IsSignedIn(User))

            return RedirectToRoleHome();



        return View(new RegisterViewModel());

    }



    // POST /Account/Register

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Register(RegisterViewModel model)

    {

        if (!ModelState.IsValid)

            return View(model);



        var normalizedEmail = model.Email.Trim().ToLowerInvariant();

        var existing = await _userManager.FindByEmailAsync(normalizedEmail);

        if (existing != null)

        {

            ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");

            return View(model);

        }



        // Strict hierarchy rule: Worker accounts require selecting a Department

        if (model.RequestedRole == "Worker" && string.IsNullOrWhiteSpace(model.DepartmentName))

        {

            ModelState.AddModelError(nameof(model.DepartmentName), "Worker accounts must be registered under a specific Department. Only Administrator is permitted at Company/Plant level.");

            return View(model);

        }



        var (resolvedCompId, resolvedPlantId, resolvedDeptId) = await _hierarchy.ResolveOrCreateHierarchyAsync(

            model.CompanyName,

            model.PlantName,

            model.DepartmentName,

            model.CompanyId,

            model.PlantId,

            model.DepartmentId);



        var requestedRole = model.RequestedRole == "Admin" ? "Admin" : "Worker";



        // Find corresponding HierarchyNode if available

        int? hierarchyNodeId = null;

        if (!string.IsNullOrWhiteSpace(model.DepartmentName))

        {

            var deptNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Department && n.IsActive &&

                (n.Name == model.DepartmentName || (resolvedDeptId.HasValue && n.Code == model.DepartmentName)));

            hierarchyNodeId = deptNode?.NodeId;

        }



        if (!hierarchyNodeId.HasValue && !string.IsNullOrWhiteSpace(model.PlantName))

        {

            var plantNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Plant && n.IsActive &&

                (n.Name == model.PlantName || (resolvedPlantId.HasValue && n.Code == model.PlantName)));

            hierarchyNodeId = plantNode?.NodeId;

        }



        if (!hierarchyNodeId.HasValue && !string.IsNullOrWhiteSpace(model.CompanyName))

        {

            var compNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => (n.NodeType == HierarchyNodeType.Enterprise || n.NodeType == HierarchyNodeType.Business) && n.IsActive &&

                (n.Name == model.CompanyName || (resolvedCompId.HasValue && n.Code == model.CompanyName)));

            hierarchyNodeId = compNode?.NodeId;

        }



        var user = new ApplicationUser

        {

            UserName = normalizedEmail,

            Email = normalizedEmail,

            FullName = model.FullName.Trim(),

            EmployeeNo = model.EmployeeNo?.Trim(),

            IsActive = false, // Disabled until admin approval

            RegistrationStatus = RegistrationStatus.Pending,

            RequestedRole = requestedRole,

            CompanyId = resolvedCompId,

            PlantId = resolvedPlantId,

            DepartmentId = resolvedDeptId,

            HierarchyNodeId = hierarchyNodeId,

            CurrentAssemblyLineId = null,

            WorkStationId = null,

            CreatedAt = DateTime.UtcNow

        };



        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)

        {

            foreach (var error in result.Errors)

            {

                ModelState.AddModelError(string.Empty, error.Description);

            }

            return View(model);

        }



        // Handle 1-click missing department request to immediate parent plant admin

        var missingDeptName = (model.MissingDepartmentName ?? model.DepartmentName)?.Trim();

        if (model.IsMissingDepartmentRequested && !string.IsNullOrWhiteSpace(missingDeptName))

        {

            // Find parent plant hierarchy node

            var plantNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Plant && n.IsActive && (n.Name == model.PlantName || n.Code == model.PlantName));

            if (plantNode != null)

            {

                await _hierarchy.RequestChildNodeAsync(plantNode.NodeId, missingDeptName, HierarchyNodeType.Department, user.Id);

                _logger.LogInformation("Recorded child department request '{Dept}' under Plant {Plant} for user {Email}", missingDeptName, plantNode.Name, user.Email);

            }

        }



        // Ensure role exists and associate

        if (!await _roleManager.RoleExistsAsync(requestedRole))

        {

            await _roleManager.CreateAsync(new IdentityRole(requestedRole));

        }

        await _userManager.AddToRoleAsync(user, requestedRole);



        _logger.LogInformation("New user registered: {Email} requesting role {Role}. Awaiting admin review.", normalizedEmail, requestedRole);



        return RedirectToAction(nameof(AccessStatus), new { email = normalizedEmail });

    }



    // GET /Account/GetHierarchyCascade

    [HttpGet]

    [AllowAnonymous]

    public async Task<IActionResult> GetHierarchyCascade()

    {

        var options = await _hierarchy.GetCascadeOptionsAsync();

        return Json(options);

    }



    // GET /Account/AccessStatus

    [HttpGet]

    public async Task<IActionResult> AccessStatus(string email)

    {

        if (string.IsNullOrWhiteSpace(email))

            return RedirectToAction(nameof(Login));



        var user = await _userManager.FindByEmailAsync(email.Trim());

        if (user == null)

            return RedirectToAction(nameof(Login));



        return View(user);

    }



    // GET or POST /Account/Logout

    [HttpGet]

    [HttpPost]

    [IgnoreAntiforgeryToken]

    public async Task<IActionResult> Logout()
    {
        var userId = _userManager.GetUserId(User);
        if (!string.IsNullOrEmpty(userId))
        {
            try
            {
                var now = DateTime.UtcNow;

                // 1. Turn off all active in-progress sessions for this worker on logout
                var activeSessions = await _db.Sessions
                    .Include(s => s.Results)
                    .Where(s => s.WorkerId == userId && s.Status == SessionStatus.InProgress && s.EndTime == null)
                    .ToListAsync();

                foreach (var s in activeSessions)
                {
                    s.TorqueDeviceId = null;
                    s.VernierDeviceId = null;
                    s.Status = SessionStatus.OnHold;
                    s.EndTime = now;

                    var hasUncorrectedFail = s.Results.Any(r => r.Result == StepResult.Fail && !s.Results.Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Pass));
                    s.FinalResult = hasUncorrectedFail ? Core.Enums.SessionResult.Fail : Core.Enums.SessionResult.Incomplete;
                }

                // 2. Close active activity windows
                var openWindows = await _db.SessionActivityWindows
                    .Where(w => w.WorkerId == userId && w.WindowEnd == null)
                    .ToListAsync();
                foreach (var w in openWindows)
                {
                    w.WindowEnd = now;
                }

                // 3. End-of-day / Logout handover: Return unfinished jobs to Open pool so they are unassigned from worker's login
                var incompleteJobs = await _db.Jobs
                    .Where(j => j.AssignedWorkerId == userId &&
                                j.Status != JobStatus.Completed &&
                                j.Status != JobStatus.Cancelled)
                    .ToListAsync();

                foreach (var j in incompleteJobs)
                {
                    j.AssignedWorkerId = null;
                    j.AssignedAt = null;
                    j.Status = JobStatus.Pending; // Return to Open pool for any worker on that line/cell
                }

                await _db.SaveChangesAsync();
                SmartTorque.Web.Areas.Worker.Controllers.JobBoardController.ClearWorkerStationTool(userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to release worker device sessions on logout");
            }
        }

        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
        return RedirectToAction(nameof(Login));
    }



    // GET /Account/AccessDenied

    [HttpGet]

    public IActionResult AccessDenied() => View();



    // ---------------------------------------------------------------

    // Helpers

    // ---------------------------------------------------------------



    private IActionResult RedirectToRoleHome()

    {

        if (User.IsInRole("Admin"))

            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });



        if (User.IsInRole("Worker"))

            return RedirectToAction("Index", "JobBoard", new { area = "Worker" });



        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

    }

}