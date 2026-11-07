using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Jobs;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// Dedicated Job Detail and Job lifecycle actions (P4.11 / Architecture §6.1).
/// </summary>
[Authorize(Roles = "Admin,Worker")]
public class JobController : Controller
{
    private readonly IJobImportService _jobs;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext? _db;
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<JobController> _logger;

    public JobController(
        IJobImportService jobs,
        UserManager<ApplicationUser> userManager,
        ILogger<JobController> logger,
        AppDbContext? db = null,
        IQrCodeService? qrCodeService = null)
    {
        _jobs = jobs;
        _userManager = userManager;
        _logger = logger;
        _db = db;
        _qrCodeService = qrCodeService ?? new QrCodeService();
    }

    // GET /Job/UnitQrCode?token=PRD-...
    [HttpGet]
    [AllowAnonymous]
    public IActionResult UnitQrCode(string token, int? pixels = 10)
    {
        if (string.IsNullOrWhiteSpace(token)) return BadRequest("Token is required.");
        var svg = _qrCodeService.GenerateSvg(token.Trim(), pixels ?? 10);
        return Content(svg, "image/svg+xml");
    }

    // GET /Job/{id}/PrintUnitQrs
    [HttpGet]
    [Route("Job/{id:int}/PrintUnitQrs")]
    [Authorize(Roles = "Admin,Worker")]
    public async Task<IActionResult> PrintUnitQrs(int id, int? unitNo = null, string? token = null)
    {
        var job = await _jobs.GetJobByIdAsync(id);
        if (job == null) return NotFound();

        if (_db != null && job.Units.Count < job.Quantity)
        {
            var existingIndices = job.Units.Select(u => u.UnitIndex).ToHashSet();
            for (int i = 1; i <= job.Quantity; i++)
            {
                if (!existingIndices.Contains(i))
                {
                    var newUnit = new JobUnit
                    {
                        JobId = job.JobId,
                        UnitIndex = i,
                        TokenNo = TokenGenerator.GenerateProductUnitToken(),
                        Status = i <= job.QuantityCompleted ? UnitAssemblyStatus.Completed : UnitAssemblyStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    };
                    job.Units.Add(newUnit);
                    _db.JobUnits.Add(newUnit);
                }
            }
            await _db.SaveChangesAsync();
        }

        var units = job.Units.OrderBy(u => u.UnitIndex).AsEnumerable();
        if (unitNo.HasValue)
        {
            units = units.Where(u => u.UnitIndex == unitNo.Value);
        }
        else if (!string.IsNullOrWhiteSpace(token))
        {
            units = units.Where(u => u.TokenNo.Equals(token.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        var vm = new PrintUnitQrsViewModel
        {
            Title = unitNo.HasValue ? $"PRD-{unitNo.Value} Label — {job.JobNo}" : $"Unit QR Code Labels — {job.JobNo}",
            Subtitle = $"Product: {job.Product.ProductCode} ({job.Product.ProductName})",
            TotalJobs = 1,
            Items = units.Select(u => new PrintUnitItemViewModel
            {
                JobId = job.JobId,
                JobNo = job.JobNo,
                ProductCode = job.Product.ProductCode,
                ProductName = job.Product.ProductName,
                UnitIndex = u.UnitIndex,
                TotalQuantity = job.Quantity,
                TokenNo = u.TokenNo,
                ProductSerial = u.ProductSerial
            }).ToList()
        };

        return View("PrintUnitQrs", vm);
    }

    // GET /Job/PrintBatchQrs or POST /Job/PrintBatchQrs
    [HttpGet]
    [HttpPost]
    [Route("Job/PrintBatchQrs")]
    [Authorize(Roles = "Admin,Worker")]
    public async Task<IActionResult> PrintBatchQrs(
        [FromQuery] string? jobIds = null,
        [FromForm(Name = "jobIds")] string? postJobIds = null,
        [FromQuery] string? tokens = null,
        [FromForm(Name = "tokens")] string? postTokens = null,
        [FromQuery] string? queue = null,
        [FromForm(Name = "queue")] string? postQueue = null,
        [FromQuery] int? departmentId = null,
        [FromForm(Name = "departmentId")] int? postDepartmentId = null)
    {
        var rawJobIds = !string.IsNullOrWhiteSpace(postJobIds) ? postJobIds : jobIds;
        var rawTokens = !string.IsNullOrWhiteSpace(postTokens) ? postTokens : tokens;
        var queueType = !string.IsNullOrWhiteSpace(postQueue) ? postQueue : queue;
        var effectiveDeptId = postDepartmentId ?? departmentId;

        var tokenList = (rawTokens ?? "")
            .Split(new[] { ',', ';', ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var idList = (rawJobIds ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.TryParse(s.Trim(), out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        var vm = new PrintUnitQrsViewModel
        {
            Title = "Batch Unit QR Code Labels"
        };

        if (_db == null)
        {
            return View("PrintUnitQrs", vm);
        }

        // Case 1: Specific tokens requested
        if (tokenList.Count > 0)
        {
            var units = await _db.JobUnits
                .Include(u => u.Job)
                    .ThenInclude(j => j.Product)
                .Where(u => tokenList.Contains(u.TokenNo))
                .OrderBy(u => u.Job.JobNo)
                .ThenBy(u => u.UnitIndex)
                .ToListAsync();

            vm.TotalJobs = units.Select(u => u.JobId).Distinct().Count();
            vm.Subtitle = $"Selected {units.Count} PRD Unit Labels across {vm.TotalJobs} Work Orders";
            vm.Items = units.Select(u => new PrintUnitItemViewModel
            {
                JobId = u.JobId,
                JobNo = u.Job.JobNo,
                ProductCode = u.Job.Product.ProductCode,
                ProductName = u.Job.Product.ProductName,
                UnitIndex = u.UnitIndex,
                TotalQuantity = u.Job.Quantity,
                TokenNo = u.TokenNo,
                ProductSerial = u.ProductSerial
            }).ToList();

            return View("PrintUnitQrs", vm);
        }

        // Case 2: Specific job IDs requested
        if (idList.Count > 0)
        {
            var jobs = await _db.Jobs
                .Include(j => j.Product)
                .Include(j => j.Units)
                .Where(j => idList.Contains(j.JobId))
                .OrderBy(j => j.JobNo)
                .ToListAsync();

            bool unitsCreated = false;
            foreach (var j in jobs)
            {
                if (j.Units.Count < j.Quantity)
                {
                    var existingIndices = j.Units.Select(u => u.UnitIndex).ToHashSet();
                    for (int i = 1; i <= j.Quantity; i++)
                    {
                        if (!existingIndices.Contains(i))
                        {
                            var newUnit = new JobUnit
                            {
                                JobId = j.JobId,
                                UnitIndex = i,
                                TokenNo = TokenGenerator.GenerateProductUnitToken(),
                                Status = i <= j.QuantityCompleted ? UnitAssemblyStatus.Completed : UnitAssemblyStatus.Pending,
                                CreatedAt = DateTime.UtcNow
                            };
                            j.Units.Add(newUnit);
                            _db.JobUnits.Add(newUnit);
                            unitsCreated = true;
                        }
                    }
                }
            }
            if (unitsCreated) await _db.SaveChangesAsync();

            vm.TotalJobs = jobs.Count;
            vm.Subtitle = $"Batch Print for {jobs.Count} Selected Work Orders";
            vm.Items = jobs.SelectMany(j => j.Units.OrderBy(u => u.UnitIndex).Select(u => new PrintUnitItemViewModel
            {
                JobId = j.JobId,
                JobNo = j.JobNo,
                ProductCode = j.Product.ProductCode,
                ProductName = j.Product.ProductName,
                UnitIndex = u.UnitIndex,
                TotalQuantity = j.Quantity,
                TokenNo = u.TokenNo,
                ProductSerial = u.ProductSerial
            })).ToList();

            return View("PrintUnitQrs", vm);
        }

        // Case 3: Entire Queue requested
        if (!string.IsNullOrEmpty(queueType))
        {
            var jobsQuery = _db.Jobs
                .Include(j => j.Product)
                .Include(j => j.AssemblyLine)
                .Include(j => j.Units)
                .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);

            if (queueType.Equals("unallocated", StringComparison.OrdinalIgnoreCase))
            {
                jobsQuery = jobsQuery.Where(j => string.IsNullOrEmpty(j.AssignedWorkerId));
            }
            else if (queueType.Equals("dispatched", StringComparison.OrdinalIgnoreCase))
            {
                jobsQuery = jobsQuery.Where(j => !string.IsNullOrEmpty(j.AssignedWorkerId));
            }

            if (effectiveDeptId.HasValue && effectiveDeptId.Value > 0)
            {
                jobsQuery = jobsQuery.Where(j => j.Product.DepartmentId == effectiveDeptId.Value ||
                    (j.AssemblyLine != null && j.AssemblyLine.DepartmentId == effectiveDeptId.Value));
            }

            var queueJobs = await jobsQuery.OrderBy(j => j.Priority).ThenBy(j => j.DueDate).ToListAsync();

            bool unitsCreated = false;
            foreach (var j in queueJobs)
            {
                if (j.Units.Count < j.Quantity)
                {
                    var existingIndices = j.Units.Select(u => u.UnitIndex).ToHashSet();
                    for (int i = 1; i <= j.Quantity; i++)
                    {
                        if (!existingIndices.Contains(i))
                        {
                            var newUnit = new JobUnit
                            {
                                JobId = j.JobId,
                                UnitIndex = i,
                                TokenNo = TokenGenerator.GenerateProductUnitToken(),
                                Status = i <= j.QuantityCompleted ? UnitAssemblyStatus.Completed : UnitAssemblyStatus.Pending,
                                CreatedAt = DateTime.UtcNow
                            };
                            j.Units.Add(newUnit);
                            _db.JobUnits.Add(newUnit);
                            unitsCreated = true;
                        }
                    }
                }
            }
            if (unitsCreated) await _db.SaveChangesAsync();

            var qName = queueType.Equals("unallocated", StringComparison.OrdinalIgnoreCase) ? "Unallocated Queue" : "Dispatched Jobs Queue";
            vm.TotalJobs = queueJobs.Count;
            vm.Title = $"Entire {qName} QR Code Labels";
            vm.Subtitle = $"Total {queueJobs.Count} Jobs &bull; {queueJobs.Sum(j => j.Quantity)} Units";
            vm.Items = queueJobs.SelectMany(j => j.Units.OrderBy(u => u.UnitIndex).Select(u => new PrintUnitItemViewModel
            {
                JobId = j.JobId,
                JobNo = j.JobNo,
                ProductCode = j.Product.ProductCode,
                ProductName = j.Product.ProductName,
                UnitIndex = u.UnitIndex,
                TotalQuantity = j.Quantity,
                TokenNo = u.TokenNo,
                ProductSerial = u.ProductSerial
            })).ToList();

            return View("PrintUnitQrs", vm);
        }

        return View("PrintUnitQrs", vm);
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