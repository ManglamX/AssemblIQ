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
        return View(new LoginViewModel());
    }

    // POST /Account/WorkerQuickLogin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkerQuickLogin(string workerId, string pin, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(workerId))
            return Json(new { success = false, message = "Please select a worker." });

        var user = await _userManager.FindByIdAsync(workerId) ?? await _userManager.FindByEmailAsync(workerId);
        if (user == null || !user.IsActive || user.RegistrationStatus != RegistrationStatus.Approved)
            return Json(new { success = false, message = "Worker account not found or inactive." });

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Worker") && !string.Equals(user.RequestedRole, "Worker", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "This quick sign-in is reserved for assembly workers. Please use the Admin / Supervisor sign-in tab." });

        // Validate PIN: accept factory quick PIN "1234", "0000", custom user PIN (stored in PhoneNumber), user's EmployeeNo, or user's account password
        bool pinValid = (pin == "1234") || (pin == "0000") || 
                        (user.EmployeeNo != null && pin == user.EmployeeNo) ||
                        (!string.IsNullOrWhiteSpace(user.PhoneNumber) && pin == user.PhoneNumber);
        if (!pinValid && !string.IsNullOrWhiteSpace(pin))
        {
            pinValid = await _userManager.CheckPasswordAsync(user, pin);
        }

        if (!pinValid)
        {
            return Json(new { success = false, message = "Incorrect PIN. Default factory worker PIN is 1234." });
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("Worker {Email} logged in via Quick PIN", user.Email);

        SmartTorque.Web.Areas.Worker.Controllers.JobBoardController.ClearWorkerStationTool(user.Id);

        var targetUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/Worker", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : Url.Action("Index", "JobBoard", new { area = "Worker" });

        return Json(new { success = true, workerName = user.FullName, redirectUrl = targetUrl });
    }

    // POST /Account/WorkerQrLogin
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> WorkerQrLogin(string qrCode, string? returnUrl = null)
    {
        if (string.IsNullOrWhiteSpace(qrCode))
            return Json(new { success = false, message = "Please enter or scan a Worker ID / Badge." });

        qrCode = qrCode.Trim();

        // Support formats: "WORKER:<Email>", "WORKER:<EmployeeNo>", "<Email>", "<EmployeeNo>", or "<UserId>"
        string query = qrCode.StartsWith("WORKER:", StringComparison.OrdinalIgnoreCase)
            ? qrCode.Substring(7).Trim()
            : qrCode;

        var lowerQuery = query.ToLowerInvariant();
        var user = await _userManager.FindByEmailAsync(query)
                   ?? await _userManager.FindByIdAsync(query)
                   ?? await _userManager.FindByNameAsync(query)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.EmployeeNo != null && u.EmployeeNo.ToLower() == lowerQuery)
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.Email != null && (u.Email.ToLower() == lowerQuery || u.Email.ToLower() == lowerQuery + "@godrej.com"))
                   ?? await _db.Users.FirstOrDefaultAsync(u => u.UserName != null && (u.UserName.ToLower() == lowerQuery || u.UserName.ToLower() == lowerQuery + "@godrej.com"));

        if (user == null || !user.IsActive || user.RegistrationStatus != RegistrationStatus.Approved)
            return Json(new { success = false, message = "Unrecognized or inactive Worker badge / ID." });

        var roles = await _userManager.GetRolesAsync(user);
        if (!roles.Contains("Worker") && !string.Equals(user.RequestedRole, "Worker", StringComparison.OrdinalIgnoreCase))
            return Json(new { success = false, message = "This ID belongs to an Admin/Supervisor. Please use the Admin / Staff tab." });

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("Worker {Email} logged in via Badge QR / ID scan", user.Email);

        SmartTorque.Web.Areas.Worker.Controllers.JobBoardController.ClearWorkerStationTool(user.Id);

        var targetUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl.StartsWith("/Worker", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : Url.Action("Index", "JobBoard", new { area = "Worker" });

        return Json(new { success = true, workerName = user.FullName, redirectUrl = targetUrl });
    }

    private async Task<List<WorkerQuickLoginDto>> GetActiveWorkersAsync()
    {
        try
        {
            var workerRoleUsers = await _userManager.GetUsersInRoleAsync("Worker");
            var activeWorkers = workerRoleUsers
                .Where(u => u.IsActive && u.RegistrationStatus == RegistrationStatus.Approved)
                .OrderBy(u => u.FullName)
                .ToList();

            var deptIds = activeWorkers.Where(w => w.DepartmentId.HasValue).Select(w => w.DepartmentId!.Value).Distinct().ToList();
            var depts = await _db.Departments.Where(d => deptIds.Contains(d.DepartmentId)).ToDictionaryAsync(d => d.DepartmentId, d => d.Name);

            var lineIds = activeWorkers.Where(w => w.CurrentAssemblyLineId.HasValue).Select(w => w.CurrentAssemblyLineId!.Value).Distinct().ToList();
            var lines = await _db.AssemblyLines.Where(l => lineIds.Contains(l.AssemblyLineId)).ToDictionaryAsync(l => l.AssemblyLineId, l => l.AssemblyCode);

            var stIds = activeWorkers.Where(w => w.WorkStationId.HasValue).Select(w => w.WorkStationId!.Value).Distinct().ToList();
            var stations = await _db.WorkStations.Where(s => stIds.Contains(s.WorkStationId)).ToDictionaryAsync(s => s.WorkStationId, s => s.StationCode);

            return activeWorkers.Select(w => new WorkerQuickLoginDto
            {
                Id = w.Id,
                FullName = string.IsNullOrWhiteSpace(w.FullName) ? (w.UserName ?? "Worker") : w.FullName,
                Email = w.Email ?? "",
                EmployeeNo = w.EmployeeNo,
                DepartmentName = w.DepartmentId.HasValue && depts.TryGetValue(w.DepartmentId.Value, out var dName) ? dName : null,
                AssemblyLineCode = w.CurrentAssemblyLineId.HasValue && lines.TryGetValue(w.CurrentAssemblyLineId.Value, out var lCode) ? lCode : null,
                StationCode = w.WorkStationId.HasValue && stations.TryGetValue(w.WorkStationId.Value, out var sCode) ? sCode : null
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not load active workers for quick login");
            return [];
        }
    }

    // POST /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (string.IsNullOrWhiteSpace(model.Email) || string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(string.Empty, "Email and Password are required.");
            return View(model);
        }

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



    private async Task PopulateWorkerOptionsAsync(RegisterViewModel vm)
    {
        try
        {
            var depts = await _db.Departments
                .Include(d => d.Plant)
                .ThenInclude(p => p.Company)
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .ToListAsync();

            vm.AvailableDepartments = depts.Select(d => new DepartmentOptionDto
            {
                Id = d.DepartmentId,
                Name = d.Name,
                PlantName = d.Plant?.Name,
                CompanyName = d.Plant?.Company?.Name,
                PlantId = d.PlantId,
                CompanyId = d.Plant?.CompanyId
            }).ToList();

            var lines = await _db.AssemblyLines
                .Where(l => l.IsActive)
                .OrderBy(l => l.AssemblyCode)
                .ToListAsync();

            vm.AvailableLines = lines.Select(l => new LineOptionDto
            {
                Id = l.AssemblyLineId,
                Code = l.AssemblyCode,
                Name = string.IsNullOrWhiteSpace(l.AssemblyName) ? l.AssemblyCode : $"{l.AssemblyCode} - {l.AssemblyName}",
                DepartmentId = l.DepartmentId
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load worker registration options");
        }
    }

    // GET /Account/Register
    [HttpGet]
    public async Task<IActionResult> Register(string? role = null)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToRoleHome();

        var vm = new RegisterViewModel();
        if (!string.IsNullOrWhiteSpace(role))
        {
            vm.RequestedRole = string.Equals(role, "Worker", StringComparison.OrdinalIgnoreCase) ? "Worker" : "Admin";
        }
        await PopulateWorkerOptionsAsync(vm);
        return View(vm);
    }

    // POST /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        bool isWorker = string.Equals(model.RequestedRole, "Worker", StringComparison.OrdinalIgnoreCase);

        if (isWorker)
        {
            // Remove validation rules not applicable to shop-floor workers
            ModelState.Remove(nameof(model.Password));
            ModelState.Remove(nameof(model.ConfirmPassword));
            ModelState.Remove(nameof(model.Email));
            ModelState.Remove(nameof(model.CompanyName));
            ModelState.Remove(nameof(model.PlantName));

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                ModelState.AddModelError(nameof(model.FullName), "Full Name is required.");
            }

            if (string.IsNullOrWhiteSpace(model.EmployeeNo))
            {
                ModelState.AddModelError(nameof(model.EmployeeNo), "Employee / Badge ID is required.");
            }
            else
            {
                var cleanEmpCheck = model.EmployeeNo.Trim();
                var existingBadge = await _userManager.Users.FirstOrDefaultAsync(u => u.EmployeeNo == cleanEmpCheck);
                if (existingBadge != null)
                {
                    ModelState.AddModelError(nameof(model.EmployeeNo), $"A user with Badge ID '{cleanEmpCheck}' is already registered.");
                }
            }

            if (!model.DepartmentId.HasValue && string.IsNullOrWhiteSpace(model.DepartmentName))
            {
                ModelState.AddModelError(nameof(model.DepartmentId), "Please select your assigned Department.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateWorkerOptionsAsync(model);
                return View(model);
            }

            // Clean sanitized employee number
            string cleanEmpNo = System.Text.RegularExpressions.Regex.Replace(model.EmployeeNo!.Trim().ToLowerInvariant(), @"[^a-z0-9]", "");
            if (string.IsNullOrWhiteSpace(cleanEmpNo))
                cleanEmpNo = Guid.NewGuid().ToString("N")[..6];

            // Resolve Department, Plant, Company
            int? resolvedCompId = model.CompanyId;
            int? resolvedPlantId = model.PlantId;
            int? resolvedDeptId = model.DepartmentId;

            if (resolvedDeptId.HasValue)
            {
                var dept = await _db.Departments.Include(d => d.Plant).FirstOrDefaultAsync(d => d.DepartmentId == resolvedDeptId.Value);
                if (dept != null)
                {
                    resolvedDeptId = dept.DepartmentId;
                    resolvedPlantId = dept.PlantId;
                    resolvedCompId = dept.Plant?.CompanyId;
                    model.DepartmentName = dept.Name;
                    model.PlantName = dept.Plant?.Name;
                }
            }
            else if (!string.IsNullOrWhiteSpace(model.DepartmentName))
            {
                var (rc, rp, rd) = await _hierarchy.ResolveOrCreateHierarchyAsync(
                    model.CompanyName, model.PlantName, model.DepartmentName,
                    model.CompanyId, model.PlantId, model.DepartmentId);
                resolvedCompId = rc;
                resolvedPlantId = rp;
                resolvedDeptId = rd;
            }

            // Auto-generate internal email: worker.{cleanEmpNo}@godrej.com
            string workerEmail = $"worker.{cleanEmpNo}@godrej.com";
            int counter = 1;
            while (await _userManager.FindByEmailAsync(workerEmail) != null)
            {
                workerEmail = $"worker.{cleanEmpNo}{counter}@godrej.com";
                counter++;
            }

            // Auto-generate strong internal password
            string autoPassword = $"Worker#{Guid.NewGuid().ToString("N")[..8]}!2026";

            // Store custom PIN in PhoneNumber (defaults to 1234)
            string workerPin = !string.IsNullOrWhiteSpace(model.Pin) ? model.Pin.Trim() : "1234";

            int? hierarchyNodeId = null;
            if (!string.IsNullOrWhiteSpace(model.DepartmentName))
            {
                var deptNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Department && n.IsActive &&
                    (n.Name == model.DepartmentName || (resolvedDeptId.HasValue && n.Code == model.DepartmentName)));
                hierarchyNodeId = deptNode?.NodeId;
            }

            var workerUser = new ApplicationUser
            {
                UserName = workerEmail,
                Email = workerEmail,
                FullName = model.FullName.Trim(),
                EmployeeNo = model.EmployeeNo?.Trim(),
                PhoneNumber = workerPin,
                IsActive = false, // Disabled until admin approval
                RegistrationStatus = RegistrationStatus.Pending,
                RequestedRole = "Worker",
                CompanyId = resolvedCompId,
                PlantId = resolvedPlantId,
                DepartmentId = resolvedDeptId,
                HierarchyNodeId = hierarchyNodeId,
                CurrentAssemblyLineId = model.AssemblyLineId,
                WorkStationId = model.WorkStationId,
                CreatedAt = DateTime.UtcNow
            };

            var createResult = await _userManager.CreateAsync(workerUser, autoPassword);
            if (!createResult.Succeeded)
            {
                foreach (var error in createResult.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                await PopulateWorkerOptionsAsync(model);
                return View(model);
            }

            if (!await _roleManager.RoleExistsAsync("Worker"))
            {
                await _roleManager.CreateAsync(new IdentityRole("Worker"));
            }
            await _userManager.AddToRoleAsync(workerUser, "Worker");

            _logger.LogInformation("New worker access requested: {Name} (Badge: {Badge}, Email: {Email}, DeptId: {DeptId}). Awaiting approval.",
                workerUser.FullName, workerUser.EmployeeNo, workerUser.Email, workerUser.DepartmentId);

            return RedirectToAction(nameof(AccessStatus), new { email = workerUser.Email });
        }

        // --- Administrator Registration Flow ---
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            ModelState.AddModelError(nameof(model.Email), "Work Email is required for Administrator accounts.");
        }
        if (string.IsNullOrWhiteSpace(model.Password))
        {
            ModelState.AddModelError(nameof(model.Password), "Password is required for Administrator accounts.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateWorkerOptionsAsync(model);
            return View(model);
        }

        var normalizedEmail = model.Email!.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");
            await PopulateWorkerOptionsAsync(model);
            return View(model);
        }

        var (adminResolvedCompId, adminResolvedPlantId, adminResolvedDeptId) = await _hierarchy.ResolveOrCreateHierarchyAsync(
            model.CompanyName,
            model.PlantName,
            model.DepartmentName,
            model.CompanyId,
            model.PlantId,
            model.DepartmentId);

        var requestedRole = "Admin";

        int? adminHierarchyNodeId = null;
        if (!string.IsNullOrWhiteSpace(model.DepartmentName))
        {
            var deptNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Department && n.IsActive &&
                (n.Name == model.DepartmentName || (adminResolvedDeptId.HasValue && n.Code == model.DepartmentName)));
            adminHierarchyNodeId = deptNode?.NodeId;
        }
        if (!adminHierarchyNodeId.HasValue && !string.IsNullOrWhiteSpace(model.PlantName))
        {
            var plantNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Plant && n.IsActive &&
                (n.Name == model.PlantName || (adminResolvedPlantId.HasValue && n.Code == model.PlantName)));
            adminHierarchyNodeId = plantNode?.NodeId;
        }
        if (!adminHierarchyNodeId.HasValue && !string.IsNullOrWhiteSpace(model.CompanyName))
        {
            var compNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => (n.NodeType == HierarchyNodeType.Enterprise || n.NodeType == HierarchyNodeType.Business) && n.IsActive &&
                (n.Name == model.CompanyName || (adminResolvedCompId.HasValue && n.Code == model.CompanyName)));
            adminHierarchyNodeId = compNode?.NodeId;
        }

        var adminUser = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            EmployeeNo = model.EmployeeNo?.Trim(),
            IsActive = false, // Disabled until admin approval
            RegistrationStatus = RegistrationStatus.Pending,
            RequestedRole = requestedRole,
            CompanyId = adminResolvedCompId,
            PlantId = adminResolvedPlantId,
            DepartmentId = adminResolvedDeptId,
            HierarchyNodeId = adminHierarchyNodeId,
            CurrentAssemblyLineId = model.AssemblyLineId,
            WorkStationId = model.WorkStationId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(adminUser, model.Password!);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            await PopulateWorkerOptionsAsync(model);
            return View(model);
        }

        // Handle 1-click missing department request to immediate parent plant admin
        var missingDeptName = (model.MissingDepartmentName ?? model.DepartmentName)?.Trim();
        if (model.IsMissingDepartmentRequested && !string.IsNullOrWhiteSpace(missingDeptName))
        {
            var plantNode = await _db.HierarchyNodes.FirstOrDefaultAsync(n => n.NodeType == HierarchyNodeType.Plant && n.IsActive && (n.Name == model.PlantName || n.Code == model.PlantName));
            if (plantNode != null)
            {
                await _hierarchy.RequestChildNodeAsync(plantNode.NodeId, missingDeptName, HierarchyNodeType.Department, adminUser.Id);
                _logger.LogInformation("Recorded child department request '{Dept}' under Plant {Plant} for user {Email}", missingDeptName, plantNode.Name, adminUser.Email);
            }
        }

        if (!await _roleManager.RoleExistsAsync(requestedRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(requestedRole));
        }
        await _userManager.AddToRoleAsync(adminUser, requestedRole);

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