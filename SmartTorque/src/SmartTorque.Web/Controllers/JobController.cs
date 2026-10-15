using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Jobs;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// Dedicated Job Detail and Job lifecycle actions (P4.11 / Architecture §6.1).
/// </summary>
[Authorize(Roles = "Admin")]
public class JobController : Controller
{
    private readonly IJobImportService _jobs;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<JobController> _logger;

    public JobController(
        IJobImportService jobs,
        UserManager<ApplicationUser> userManager,
        ILogger<JobController> logger)
    {
        _jobs = jobs;
        _userManager = userManager;
        _logger = logger;
    }

    // GET /Job/Detail/{id} or /Job/{id}
    [HttpGet]
    [Route("Job/Detail/{id:int}")]
    [Route("Job/{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var job = await _jobs.GetJobByIdAsync(id);
        if (job == null) return NotFound();

        var auditLogs = await _jobs.GetJobAuditLogsAsync(id);

        var workers = await _userManager.GetUsersInRoleAsync("Worker");
        var workerItems = workers
            .Where(w => w.IsActive)
            .OrderBy(w => w.FullName)
            .Select(w => new SelectListItem
            {
                Value = w.Id,
                Text = $"{w.FullName} ({w.UserName})",
                Selected = w.Id == job.AssignedWorkerId
            })
            .ToList();

        var vm = new JobDetailViewModel
        {
            Job = job,
            AuditLogs = auditLogs,
            AvailableWorkers = workerItems
        };

        ViewData["Title"] = $"Job {job.JobNo}";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // POST /Job/Assign
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(int jobId, string? workerId, int? priority)
    {
        try
        {
            var p = priority.HasValue && Enum.IsDefined(typeof(PriorityLevel), (byte)priority.Value)
                ? (PriorityLevel?)priority.Value
                : null;

            var job = await _jobs.AssignJobAsync(jobId, string.IsNullOrWhiteSpace(workerId) ? null : workerId, p, UserName());
            TempData["Success"] = $"Job {job.JobNo} updated: {(job.AssignedWorkerId != null ? "Assigned" : "Unassigned (Open)")}.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to assign job {Id}", jobId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id = jobId });
    }

    // POST /Job/Hold
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int jobId, string? reason)
    {
        try
        {
            var job = await _jobs.HoldJobAsync(jobId, reason, UserName());
            TempData["Success"] = $"Job {job.JobNo} placed on hold.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to hold job {Id}", jobId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id = jobId });
    }

    // POST /Job/Resume
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Resume(int jobId)
    {
        try
        {
            var job = await _jobs.ResumeJobAsync(jobId, UserName());
            TempData["Success"] = $"Job {job.JobNo} resumed (Status: {job.Status}).";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resume job {Id}", jobId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id = jobId });
    }

    // POST /Job/Cancel
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int jobId, string? reason)
    {
        try
        {
            var job = await _jobs.CancelJobAsync(jobId, reason, UserName());
            TempData["Success"] = $"Job {job.JobNo} has been cancelled.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to cancel job {Id}", jobId);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Detail), new { id = jobId });
    }

    private string UserName() => User.Identity?.Name ?? "system";
}