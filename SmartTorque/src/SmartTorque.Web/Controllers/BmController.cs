using System.Globalization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Bm;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// The "BM & Workload" section. P4.5 covers the BM import hub (upload + preview);
/// P4.6 confirms import to Jobs; P4.7 adds PriorityService ordering; P4.8 is the
/// workload board (Black pinned, 4px left border) with filters and assignment.
/// </summary>
[Authorize(Roles = "Admin")]
public class BmController : Controller
{
    private readonly IBmPreviewService _previews;
    private readonly IJobImportService _jobs;
    private readonly IColourMapService _colours;
    private readonly IBmImportService _import;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BmController> _logger;

    /// <summary>Filter value that selects jobs with no worker (Design 7.7 "Open").</summary>
    private const string OpenWorkerValue = "open";

    public BmController(
        IBmPreviewService previews,
        IJobImportService jobs,
        IColourMapService colours,
        IBmImportService import,
        UserManager<ApplicationUser> userManager,
        ILogger<BmController> logger)
    {
        _previews = previews;
        _jobs = jobs;
        _colours = colours;
        _import = import;
        _userManager = userManager;
        _logger = logger;
    }

    // GET /Bm
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var uploads = await _previews.RecentAsync();

        var vm = new BmIndexViewModel
        {
            Legend = await _colours.GetActiveAsync(),
            RecentUploads = uploads.Select(u => new BmUploadRowViewModel
            {
                UploadId = u.UploadId,
                FileName = u.FileName,
                UploadedAt = u.UploadedAt,
                UploadedBy = u.UploadedBy,
                Status = u.Status,
                RowCount = u.RowCount,
                ErrorCount = u.ErrorCount
            }).ToList()
        };

        ViewData["Title"] = "BM import";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // POST /Bm/InspectHeaders
    [HttpPost]
    public async Task<IActionResult> InspectHeaders(IFormFile? file)
    {
        if (file == null || file.Length == 0)
            return BadRequest(new { message = "Choose a .xlsx workbook." });

        try
        {
            await using var stream = file.OpenReadStream();
            var inspection = await _import.InspectHeadersAsync(stream);
            return Json(new
            {
                success = true,
                sheetName = inspection.SheetName,
                headers = inspection.AllHeaders,
                itemCodeColumn = inspection.DetectedItemCodeColumn ?? "Item Code",
                suggestedDescription = inspection.SuggestedDescriptionColumn ?? "Finished Good Item Desc",
                suggestedQuantity = inspection.DetectedQuantityColumn ?? "Qty.Reqd",
                suggestedDueDate = inspection.DetectedDueDateColumn ?? "Due Date"
            });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    // POST /Bm/Upload
    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile? file,
        string? itemCodeColumn,
        string? descriptionColumn,
        string? quantityColumn,
        string? dueDateColumn)
    {
        if (file == null || file.Length == 0)
        {
            TempData["Error"] = "Choose a .xlsx workbook to upload.";
            return RedirectToAction(nameof(Index));
        }

        // If description column was not specified (e.g. direct non-JS form submit),
        // ask for the description column before rendering the excel.
        if (string.IsNullOrWhiteSpace(descriptionColumn))
        {
            await using var inspectStream = file.OpenReadStream();
            var inspection = await _import.InspectHeadersAsync(inspectStream);

            if (inspection.AllHeaders.Count > 0)
            {
                var tempFileName = Guid.NewGuid().ToString("N") + ".xlsx";
                var tempDir = Path.Combine(Path.GetTempPath(), "SmartTorqueUploads");
                Directory.CreateDirectory(tempDir);
                var tempPath = Path.Combine(tempDir, tempFileName);
                await using (var fs = System.IO.File.Create(tempPath))
                {
                    file.OpenReadStream().CopyTo(fs);
                }

                var mapVm = new BmMapColumnsViewModel
                {
                    TempFileId = tempFileName,
                    FileName = file.FileName,
                    FileSizeBytes = file.Length,
                    Headers = inspection.AllHeaders,
                    DetectedItemCodeColumn = inspection.DetectedItemCodeColumn ?? "Item Code",
                    SelectedDescriptionColumn = inspection.SuggestedDescriptionColumn,
                    SelectedQuantityColumn = inspection.DetectedQuantityColumn,
                    SelectedDueDateColumn = inspection.DetectedDueDateColumn
                };

                ViewData["Title"] = $"Select Description Column — {file.FileName}";
                ViewData["ActiveNav"] = "Workload";
                return View("MapColumns", mapVm);
            }
        }

        try
        {
            var customMap = new BmHeaderMap
            {
                ProductCode = string.IsNullOrWhiteSpace(itemCodeColumn) ? "Item Code" : itemCodeColumn.Trim(),
                Remarks = string.IsNullOrWhiteSpace(descriptionColumn) ? "Finished Good Item Desc" : descriptionColumn.Trim(),
                Quantity = string.IsNullOrWhiteSpace(quantityColumn) ? "Quantity" : quantityColumn.Trim(),
                DueDate = string.IsNullOrWhiteSpace(dueDateColumn) ? "DueDate" : dueDateColumn.Trim()
            };

            await using var stream = file.OpenReadStream();
            var upload = await _previews.UploadAsync(stream, file.FileName, file.Length, UserName(), customMap);

            _logger.LogInformation(
                "BM upload {Id} ({File}) by {User}: {Rows} rows, {Errors} errors. Mapped: Code='{Code}', Desc='{Desc}'",
                upload.UploadId, upload.FileName, UserName(), upload.RowCount, upload.ErrorCount, customMap.ProductCode, customMap.Remarks);

            return RedirectToAction(nameof(Preview), new { id = upload.UploadId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // POST /Bm/ProcessPendingUpload
    [HttpPost]
    public async Task<IActionResult> ProcessPendingUpload(
        string tempFileId,
        string fileName,
        string? itemCodeColumn,
        string? descriptionColumn,
        string? quantityColumn,
        string? dueDateColumn)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SmartTorqueUploads");
        var tempPath = Path.Combine(tempDir, tempFileId);

        if (!System.IO.File.Exists(tempPath))
        {
            TempData["Error"] = "The uploaded file session expired. Please upload the file again.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var fileBytes = await System.IO.File.ReadAllBytesAsync(tempPath);
            try { System.IO.File.Delete(tempPath); } catch { /* Ignore */ }

            var customMap = new BmHeaderMap
            {
                ProductCode = string.IsNullOrWhiteSpace(itemCodeColumn) ? "Item Code" : itemCodeColumn.Trim(),
                Remarks = string.IsNullOrWhiteSpace(descriptionColumn) ? "Finished Good Item Desc" : descriptionColumn.Trim(),
                Quantity = string.IsNullOrWhiteSpace(quantityColumn) ? "Quantity" : quantityColumn.Trim(),
                DueDate = string.IsNullOrWhiteSpace(dueDateColumn) ? "DueDate" : dueDateColumn.Trim()
            };

            using var stream = new MemoryStream(fileBytes);
            var upload = await _previews.UploadAsync(stream, fileName, fileBytes.Length, UserName(), customMap);

            _logger.LogInformation(
                "BM pending upload {Id} ({File}) by {User}: {Rows} rows. Code='{Code}', Desc='{Desc}'",
                upload.UploadId, upload.FileName, UserName(), upload.RowCount, customMap.ProductCode, customMap.Remarks);

            return RedirectToAction(nameof(Preview), new { id = upload.UploadId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Index));
        }
    }

    // GET /Bm/Preview/{id}
    [HttpGet]
    public async Task<IActionResult> Preview(int id)
    {
        var upload = await _previews.GetAsync(id);
        if (upload == null) return NotFound();

        var vm = new BmPreviewViewModel
        {
            UploadId = upload.UploadId,
            FileName = upload.FileName,
            UploadedAt = upload.UploadedAt,
            UploadedBy = upload.UploadedBy,
            Status = upload.Status,
            Legend = await _colours.GetActiveAsync(),
            Rows = upload.Rows
                .OrderBy(r => r.ExcelRowNo)
                .Select(r => new BmRowViewModel
                {
                    RowId = r.RowId,
                    ExcelRowNo = r.ExcelRowNo,
                    ProductCode = r.ProductCode,
                    Quantity = r.Quantity,
                    DueDate = r.DueDate,
                    Remarks = r.Remarks,
                    ColourHex = r.ColourHex,
                    Priority = r.Priority,
                    ImportStatus = r.ImportStatus,
                    ImportMessage = r.ImportMessage
                }).ToList()
        };

        // A failed upload keeps no row text, so read the errors back off the file.
        if (upload.Status == BmUploadStatus.Failed)
            vm.FileErrors = await _previews.ReadFileErrorsAsync(upload);

        // An imported upload shows the jobs it produced.
        if (upload.Status == BmUploadStatus.Confirmed)
            vm.JobNumbers = (await _jobs.GetJobNumbersAsync(upload.UploadId)).ToList();

        ViewData["Title"] = $"Preview — {upload.FileName}";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // POST /Bm/Confirm
    [HttpPost]
    public async Task<IActionResult> Confirm(int id)
    {
        try
        {
            var result = await _jobs.ConfirmAsync(id, UserName());

            _logger.LogInformation(
                "BM upload {Id} confirmed by {User}: {Count} jobs created ({Black} Black, notification raised: {Notified})",
                id, UserName(), result.JobsCreated, result.BlackJobs, result.NotificationRaised);

            TempData["Success"] =
                $"Imported {result.JobsCreated} job(s). See them listed on this preview.";
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("BM upload {Id} could not be confirmed by {User}: {Reason}", id, UserName(), ex.Message);
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Preview), new { id });
    }

    // POST /Bm/SetRowPriority
    [HttpPost]
    public async Task<IActionResult> SetRowPriority(int rowId, int priority, int uploadId)
    {
        try
        {
            await _previews.SetRowPriorityAsync(rowId, (PriorityLevel)priority);
            TempData["Success"] = "Priority updated.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Preview), new { id = uploadId });
    }

    // POST /Bm/SetRowSkipped
    [HttpPost]
    public async Task<IActionResult> SetRowSkipped(int rowId, bool skipped, int uploadId)
    {
        try
        {
            await _previews.SetRowSkippedAsync(rowId, skipped);
            TempData["Success"] = skipped ? "Row skipped." : "Row restored.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Preview), new { id = uploadId });
    }

    // POST /Bm/Cancel
    [HttpPost]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _previews.CancelAsync(id);
            _logger.LogInformation("BM upload {Id} cancelled by {User}", id, UserName());
            TempData["Success"] = "Upload cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    // GET /Bm/Workload
    [HttpGet]
    public async Task<IActionResult> Workload(
        int[]? priorities,
        string? dueFrom,
        string? dueTo,
        string? worker,
        string? status,
        int page = 1,
        int pageSize = 50)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var selectedPriorities = new List<int>();
        foreach (var value in priorities ?? [])
        {
            if (Enum.TryParse<PriorityLevel>(value.ToString(), out var level) &&
                Enum.IsDefined(level) &&
                !selectedPriorities.Contains((int)level))
            {
                selectedPriorities.Add((int)level);
            }
        }

        var filter = new WorkloadFilter
        {
            Priorities = selectedPriorities.Select(v => (PriorityLevel)v).ToList(),
            DueFrom = ParseIsoDate(dueFrom),
            DueTo = ParseIsoDate(dueTo),
            Status = ParseJobStatus(status)
        };

        if (worker == OpenWorkerValue)
            filter = filter with { UnassignedOnly = true };
        else if (!string.IsNullOrWhiteSpace(worker))
            filter = filter with { AssignedWorkerId = worker };

        var result = await _jobs.GetWorkloadAsync(filter, page, pageSize);

        var vm = new WorkloadViewModel
        {
            Jobs = result.Jobs.ToList(),
            Total = result.Total,
            Page = page,
            PageSize = pageSize,
            SelectedPriorities = selectedPriorities,
            DueFrom = dueFrom,
            DueTo = dueTo,
            Worker = worker,
            Status = status
        };

        // A stale bookmark may point past the last page — show the last page
        // rather than an empty table under a "page 99 of 1" heading.
        if (vm.Page > vm.TotalPages)
        {
            vm.Page = vm.TotalPages;
            result = await _jobs.GetWorkloadAsync(filter, vm.Page, pageSize);
            vm.Jobs = result.Jobs.ToList();
        }

        vm.Statuses.Add(new SelectListItem { Value = "", Text = "All statuses" });
        foreach (JobStatus s in Enum.GetValues<JobStatus>())
            vm.Statuses.Add(new SelectListItem { Value = ((int)s).ToString(), Text = s.ToString() });

        vm.Workers.Add(new SelectListItem { Value = "", Text = "All workers" });
        vm.Workers.Add(new SelectListItem { Value = OpenWorkerValue, Text = "Open (unassigned)" });
        var workers = await _userManager.GetUsersInRoleAsync("Worker");
        foreach (var w in workers.OrderBy(w => w.UserName, StringComparer.OrdinalIgnoreCase))
            vm.Workers.Add(new SelectListItem { Value = w.Id, Text = w.UserName ?? w.Email ?? w.Id });

        ViewData["Title"] = "Workload Board";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // GET /Bm/ByItem
    [HttpGet]
    public async Task<IActionResult> ByItem(string? search)
    {
        var items = await _jobs.GetWorkloadByItemAsync(search);

        var vm = new WorkloadByItemViewModel
        {
            Items = items,
            Search = search
        };

        ViewData["Title"] = "Jobs by Item Code";
        ViewData["ActiveNav"] = "Workload";
        return View(vm);
    }

    // POST /Bm/AssignJob
    [HttpPost]
    public async Task<IActionResult> AssignJob(int jobId, string? workerId, int? priority)
    {
        try
        {
            PriorityLevel? pri = priority.HasValue && Enum.IsDefined(typeof(PriorityLevel), (PriorityLevel)priority.Value)
                ? (PriorityLevel)priority.Value
                : null;

            var job = await _jobs.AssignJobAsync(jobId, workerId, pri, UserName());
            TempData["Success"] = string.IsNullOrEmpty(workerId)
                ? $"Job {job.JobNo} unassigned (set to Open)."
                : $"Job {job.JobNo} assigned successfully.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Workload));
    }

    // POST /Bm/AutoAssign
    [HttpPost]
    public async Task<IActionResult> AutoAssign()
    {
        try
        {
            var count = await _jobs.AutoAssignJobsAsync(UserName());
            if (count > 0)
                TempData["Success"] = $"Auto-assigned {count} open job(s) across active workers balanced by workload.";
            else
                TempData["Info"] = "No open jobs available to auto-assign.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Workload));
    }

    // POST /Bm/HoldJob
    [HttpPost]
    public async Task<IActionResult> HoldJob(int jobId, string? reason)
    {
        try
        {
            var job = await _jobs.HoldJobAsync(jobId, reason, UserName());
            TempData["Success"] = $"Job {job.JobNo} placed on hold.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Workload));
    }

    // POST /Bm/ResumeJob
    [HttpPost]
    public async Task<IActionResult> ResumeJob(int jobId)
    {
        try
        {
            var job = await _jobs.ResumeJobAsync(jobId, UserName());
            TempData["Success"] = $"Job {job.JobNo} resumed (Status: {job.Status}).";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Workload));
    }

    // POST /Bm/CancelJob
    [HttpPost]
    public async Task<IActionResult> CancelJob(int jobId, string? reason)
    {
        try
        {
            var job = await _jobs.CancelJobAsync(jobId, reason, UserName());
            TempData["Success"] = $"Job {job.JobNo} cancelled.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }

        return RedirectToAction(nameof(Workload));
    }

    // ================================================================

    /// <summary>Filter bar posts calendar dates as <c>yyyy-MM-dd</c>.</summary>
    private static DateOnly? ParseIsoDate(string? raw)
        => DateOnly.TryParseExact(
               raw,
               "yyyy-MM-dd",
               CultureInfo.InvariantCulture,
               DateTimeStyles.None,
               out var date)
            ? date
            : null;

    private static JobStatus? ParseJobStatus(string? raw)
        => int.TryParse(raw, out var value) &&
           Enum.TryParse<JobStatus>(value.ToString(), out var status) &&
           Enum.IsDefined(status)
            ? status
            : null;

    private string UserName() => User.Identity?.Name ?? "system";
}