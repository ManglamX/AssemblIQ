using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Assembly;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Assembly")]
public class AssemblyController : Controller
{
    private readonly IAssemblyService _assembly;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<AssemblyController> _logger;

    public AssemblyController(
        IAssemblyService assembly,
        UserManager<ApplicationUser> userManager,
        ILogger<AssemblyController> logger)
    {
        _assembly = assembly;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet("")]
    [HttpGet("Index")]
    public async Task<IActionResult> Index(string? search = null, string? department = null, bool? isActive = null)
    {
        ViewData["ActiveNav"] = "Assembly";

        var dashboard = await _assembly.GetDashboardStatsAsync();
        var allLines = await _assembly.GetAssemblyLinesAsync();
        var departments = allLines.Select(l => l.Department).Distinct().OrderBy(d => d).ToList();

        // If filtering applies to lines list
        var filteredLines = await _assembly.GetAssemblyLinesAsync(search, department, isActive);

        var vm = new AssemblyIndexViewModel
        {
            Dashboard = new AssemblyDashboardDto(
                dashboard.TotalAssemblyLines,
                dashboard.ActiveAssemblyLines,
                dashboard.InactiveAssemblyLines,
                dashboard.TotalMappedProducts,
                dashboard.UnmappedProductsCount,
                dashboard.TotalActiveWorkers,
                dashboard.WorkersAssignedToLines,
                dashboard.TotalPendingTasks,
                dashboard.TotalInProgressTasks,
                filteredLines),
            Search = search,
            Department = department,
            IsActive = isActive,
            Departments = departments
        };

        return View(vm);
    }

    [HttpGet("Detail/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        ViewData["ActiveNav"] = "Assembly";

        var detail = await _assembly.GetAssemblyLineDetailAsync(id);
        if (detail == null)
        {
            TempData["Error"] = $"Assembly Line {id} not found.";
            return RedirectToAction(nameof(Index));
        }

        var unmappedProducts = await _assembly.GetUnmappedProductsAsync();
        ViewBag.UnmappedProducts = unmappedProducts;

        var allWorkers = await _userManager.GetUsersInRoleAsync("Worker");
        ViewBag.ActiveWorkers = allWorkers.Where(w => w.IsActive).OrderBy(w => w.FullName).ToList();

        return View(detail);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["ActiveNav"] = "Assembly";
        return View("Form", new AssemblyLineFormViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AssemblyLineFormViewModel vm)
    {
        ViewData["ActiveNav"] = "Assembly";

        if (!ModelState.IsValid)
        {
            return View("Form", vm);
        }

        var unique = await _assembly.IsCodeUniqueAsync(vm.AssemblyCode);
        if (!unique)
        {
            ModelState.AddModelError(nameof(vm.AssemblyCode), $"Assembly ID '{vm.AssemblyCode}' is already in use.");
            return View("Form", vm);
        }

        try
        {
            var line = await _assembly.CreateAssemblyLineAsync(
                new CreateAssemblyLineRequest(vm.AssemblyCode, vm.AssemblyName, vm.Department, vm.Description, vm.IsActive),
                User.Identity?.Name ?? "Admin");

            TempData["Success"] = $"Assembly Line '{line.AssemblyCode}' created successfully.";
            return RedirectToAction(nameof(Detail), new { id = line.AssemblyLineId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create assembly line");
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", vm);
        }
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["ActiveNav"] = "Assembly";

        var detail = await _assembly.GetAssemblyLineDetailAsync(id);
        if (detail == null) return NotFound();

        var vm = new AssemblyLineFormViewModel
        {
            AssemblyLineId = detail.AssemblyLineId,
            AssemblyCode = detail.AssemblyCode,
            AssemblyName = detail.AssemblyName,
            Department = detail.Department,
            Description = detail.Description,
            IsActive = detail.IsActive
        };

        return View("Form", vm);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, AssemblyLineFormViewModel vm)
    {
        ViewData["ActiveNav"] = "Assembly";

        if (!ModelState.IsValid)
        {
            return View("Form", vm);
        }

        var unique = await _assembly.IsCodeUniqueAsync(vm.AssemblyCode, id);
        if (!unique)
        {
            ModelState.AddModelError(nameof(vm.AssemblyCode), $"Assembly ID '{vm.AssemblyCode}' is already in use.");
            return View("Form", vm);
        }

        try
        {
            await _assembly.UpdateAssemblyLineAsync(
                id,
                new UpdateAssemblyLineRequest(vm.AssemblyCode, vm.AssemblyName, vm.Department, vm.Description, vm.IsActive),
                User.Identity?.Name ?? "Admin");

            TempData["Success"] = $"Assembly Line '{vm.AssemblyCode}' updated successfully.";
            return RedirectToAction(nameof(Detail), new { id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update assembly line {Id}", id);
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", vm);
        }
    }

    [HttpPost("ToggleStatus/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(int id, string? returnUrl = null)
    {
        try
        {
            var active = await _assembly.ToggleAssemblyLineStatusAsync(id, User.Identity?.Name ?? "Admin");
            TempData["Success"] = $"Assembly line status changed to {(active ? "Active" : "Inactive")}.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        try
        {
            var hardDeleted = await _assembly.DeleteAssemblyLineAsync(id, User.Identity?.Name ?? "Admin");
            if (hardDeleted)
                TempData["Success"] = "Assembly line deleted successfully.";
            else
                TempData["Success"] = "Assembly line deactivated (tasks/history preserved).";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("ProductMappings")]
    public async Task<IActionResult> ProductMappings(string? search = null, int? assemblyLineId = null, bool? unmappedOnly = null)
    {
        ViewData["ActiveNav"] = "Assembly";

        var products = await _assembly.GetAllProductsWithLineMappingAsync(search, assemblyLineId, unmappedOnly);
        var lines = await _assembly.GetAssemblyLinesAsync(isActive: true);
        var unmappedList = await _assembly.GetUnmappedProductsAsync();

        var vm = new ProductMappingsViewModel
        {
            Products = products,
            AssemblyLines = lines,
            Search = search,
            AssemblyLineId = assemblyLineId,
            UnmappedOnly = unmappedOnly,
            UnmappedCount = unmappedList.Count
        };

        return View(vm);
    }

    [HttpPost("AssignProduct")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignProduct(int productId, int assemblyLineId, bool isDefault = true, string? returnUrl = null)
    {
        try
        {
            await _assembly.AssignProductToLineAsync(productId, assemblyLineId, isDefault, User.Identity?.Name ?? "Admin");
            TempData["Success"] = "Product mapped to assembly line successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ProductMappings), new { assemblyLineId });
    }

    [HttpPost("RemoveProduct")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveProduct(int productId, int assemblyLineId, string? returnUrl = null)
    {
        try
        {
            await _assembly.RemoveProductFromLineAsync(productId, assemblyLineId, User.Identity?.Name ?? "Admin");
            TempData["Success"] = "Product mapping removed.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ProductMappings));
    }

    [HttpPost("SetDefaultLine")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetDefaultLine(int productId, int assemblyLineId, string? returnUrl = null)
    {
        try
        {
            await _assembly.SetDefaultLineForProductAsync(productId, assemblyLineId, User.Identity?.Name ?? "Admin");
            TempData["Success"] = "Designated default assembly line updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(ProductMappings));
    }

    [HttpGet("Workers")]
    public async Task<IActionResult> Workers()
    {
        ViewData["ActiveNav"] = "Assembly";

        var workers = await _assembly.GetAllActiveWorkersWithLineAsync();
        var lines = await _assembly.GetAssemblyLinesAsync(isActive: true);

        var vm = new WorkersManagementViewModel
        {
            Workers = workers,
            AssemblyLines = lines
        };

        return View(vm);
    }

    [HttpPost("AssignWorkerLine")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignWorkerLine(string workerId, int? assemblyLineId, string? returnUrl = null)
    {
        try
        {
            await _assembly.SetWorkerAssemblyLineAsync(workerId, assemblyLineId, User.Identity?.Name ?? "Admin");
            TempData["Success"] = "Worker assembly line updated.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Workers));
    }

    [HttpPost("AutoAssign")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AutoAssign(int? assemblyLineId = null, string? returnUrl = null)
    {
        try
        {
            var result = await _assembly.AutoAssignJobsAsync(User.Identity?.Name ?? "Admin", assemblyLineId);

            if (result.TotalAssigned > 0)
            {
                TempData["Success"] = $"Auto-allocated {result.TotalAssigned} task(s) based on worker workload.";
            }
            else if (result.TotalUnassigned > 0)
            {
                TempData["Warning"] = $"{result.TotalUnassigned} task(s) could not be assigned (no active workers on the mapped assembly lines). Alert generated for Admin.";
            }
            else
            {
                TempData["Info"] = "No open unassigned tasks found for auto-assignment.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AutoAssign failed");
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ReassignTask")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReassignTask(
        int jobId,
        string? workerId,
        decimal? estimatedWorkload,
        int? assemblyLineId,
        string? returnUrl = null)
    {
        try
        {
            await _assembly.ManualReassignJobAsync(jobId, workerId, estimatedWorkload, assemblyLineId, User.Identity?.Name ?? "Admin");
            TempData["Success"] = "Task reassigned successfully.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = ex.Message;
        }

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);

        return RedirectToAction(nameof(Index));
    }
}