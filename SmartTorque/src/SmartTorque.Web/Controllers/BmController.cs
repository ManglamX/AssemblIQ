using System.Globalization;



using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.Mvc.Rendering;

using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;



using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using SmartTorque.Web.ViewModels.Bm;



namespace SmartTorque.Web.Controllers;

/// <summary>Request model for updating job quantity.</summary>
public record UpdateJobQuantityRequest(int JobId, int Quantity);

/// <summary>Request model for batch updating job quantities.</summary>
public record UpdateJobQuantitiesBatchRequest(List<JobQuantityChange> Changes);

/// <summary>Individual job quantity change.</summary>
public record JobQuantityChange(int JobId, int Quantity);

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

    private readonly IHubContext<SmartTorque.Web.Hubs.LiveReadingHub>? _hubContext;

    private readonly IUserContextService? _userContext;
    private readonly IHierarchyService? _hierarchy;
    private readonly AppDbContext? _db;
    private readonly ILogger<BmController> _logger;

    /// <summary>Filter value that selects jobs with no worker (Design 7.7 "Open").</summary>
    private const string OpenWorkerValue = "open";

    public BmController(
    IBmPreviewService previews,
    IJobImportService jobs,
    IColourMapService colours,
    IBmImportService import,
    UserManager<ApplicationUser> userManager,
    IUserContextService? userContext,
    IHierarchyService? hierarchy,
    ILogger<BmController> logger,
    IHubContext<SmartTorque.Web.Hubs.LiveReadingHub>? hubContext = null,
    AppDbContext? db = null)
    {
        _previews = previews;
        _jobs = jobs;
        _colours = colours;
        _import = import;
        _userManager = userManager;
        _userContext = userContext;
        _hierarchy = hierarchy;
        _logger = logger;
        _hubContext = hubContext;
        _db = db;
    }

    // GET /Bm

    [HttpGet]

    public async Task<IActionResult> Index()

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;
        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();

        var uploads = await _previews.RecentAsync();
        if (!isPlantHead)
        {
            var userEmail = currentUserCtx?.Email;
            var userName = User.Identity?.Name;
            var deptUserNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (userEmail != null) deptUserNames.Add(userEmail);
            if (userName != null) deptUserNames.Add(userName);

            if (effectiveDeptId.HasValue)
            {
                var usersInDept = await _userManager.Users.Where(u => u.DepartmentId == effectiveDeptId.Value).ToListAsync();
                foreach (var u in usersInDept)
                {
                    if (!string.IsNullOrEmpty(u.UserName)) deptUserNames.Add(u.UserName);
                    if (!string.IsNullOrEmpty(u.Email)) deptUserNames.Add(u.Email);
                }
            }

            uploads = uploads.Where(u => !string.IsNullOrEmpty(u.UploadedBy) && deptUserNames.Contains(u.UploadedBy)).ToList();
        }

        var productOptions = await _jobs.GetActiveProductOptionsAsync(effectiveDeptId);

        var lineOptions = await _jobs.GetActiveAssemblyLineOptionsAsync(effectiveDeptId);



        var workers = await _userManager.GetUsersInRoleAsync("Worker");
        if (!isPlantHead && effectiveDeptId.HasValue)
        {
            workers = workers.Where(w => w.DepartmentId == effectiveDeptId.Value).ToList();
        }

        var workerList = workers

            .Where(w => w.IsActive)

            .OrderBy(w => w.FullName ?? w.UserName, StringComparer.OrdinalIgnoreCase)

            .Select(w => new SelectListItem

            {

                Value = w.Id,

                Text = $"{w.FullName ?? w.UserName} ({w.Email ?? w.UserName})"

            })

            .ToList();



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

            }).ToList(),

            AvailableProducts = productOptions.Select(p => new ProductOptionViewModel

            {

                ProductId = p.ProductId,

                ProductCode = p.ProductCode,

                Name = p.ProductName,

                DefaultAssemblyLineId = p.DefaultAssemblyLineId,

                DefaultAssemblyName = p.DefaultAssemblyName

            }).ToList(),

            AvailableAssemblyLines = lineOptions.Select(l => new AssemblyLineOptionViewModel

            {

                AssemblyLineId = l.AssemblyLineId,

                AssemblyCode = l.AssemblyCode,
                AssemblyName = l.AssemblyName,

                Department = l.Department

            }).ToList(),

            AvailableWorkers = workerList

        };



        ViewData["Title"] = "BM import";

        ViewData["ActiveNav"] = "Workload";

        return View(vm);

    }



    // POST /Bm/CreateWorkload

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> CreateWorkload(CreateManualWorkloadViewModel model)

    {

        if (!ModelState.IsValid)

        {

            var err = string.Join(" ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)

                return BadRequest(new { success = false, message = err });



            TempData["Error"] = err;

            return RedirectToAction(nameof(Index));

        }



        try

        {

            var req = new CreateManualWorkloadRequest(

                ProductCode: model.ProductCode,

                Quantity: model.Quantity,

                DueDate: model.DueDate,

                Priority: model.Priority,

                SalesOrder: model.SalesOrder,

                PONo: model.PONo,

                Remarks: model.Remarks,

                AssemblyLineId: model.AssemblyLineId,

                AssignedWorkerId: model.AssignedWorkerId,

                EstimatedWorkload: model.EstimatedWorkload

            );



            var job = await _jobs.CreateManualWorkloadAsync(req, UserName());



            if (job.Priority == PriorityLevel.Black && _hubContext != null)

            {

                await _hubContext.Clients.Group("workers").SendAsync("BlackJobAlert", new

                {

                    message = $"Urgent Black priority workload {job.JobNo} ({model.ProductCode}) requires immediate production!"

                });

            }



            var successMsg = $"Workload '{job.JobNo}' successfully created for {job.Quantity} unit(s) of '{model.ProductCode}'. Generated {job.Quantity} individual unit tracking token(s).";



            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)

            {

                return Json(new { success = true, jobNo = job.JobNo, quantity = job.Quantity, message = successMsg });

            }



            TempData["Success"] = successMsg;

        }

        catch (InvalidOperationException ex)

        {

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)

                return BadRequest(new { success = false, message = ex.Message });



            TempData["Error"] = ex.Message;

        }

        catch (Exception ex)

        {

            _logger.LogError(ex, "Failed to create manual workload for product {Code}", model.ProductCode);

            var err = $"An error occurred while creating workload: {ex.Message}";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.ContentType?.Contains("application/json") == true)

                return StatusCode(500, new { success = false, message = err });



            TempData["Error"] = err;

        }



        return RedirectToAction(nameof(Index));

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

                suggestedDueDate = inspection.DetectedDueDateColumn ?? "Due Date",

                suggestedSalesOrder = inspection.DetectedSalesOrderColumn ?? "Sales Order",

                suggestedPoNo = inspection.DetectedPONoColumn ?? "PONo"

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

        string? dueDateColumn,

        string? salesOrderColumn = null,

        string? poNoColumn = null)

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

                    SelectedDueDateColumn = inspection.DetectedDueDateColumn,

                    DetectedSalesOrderColumn = inspection.DetectedSalesOrderColumn ?? "Sales Order",

                    DetectedPONoColumn = inspection.DetectedPONoColumn ?? "PONo",

                    SelectedSalesOrderColumn = inspection.DetectedSalesOrderColumn ?? "Sales Order",

                    SelectedPONoColumn = inspection.DetectedPONoColumn ?? "PONo"

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

                DueDate = string.IsNullOrWhiteSpace(dueDateColumn) ? "DueDate" : dueDateColumn.Trim(),

                SalesOrder = string.IsNullOrWhiteSpace(salesOrderColumn) ? "Sales Order" : salesOrderColumn.Trim(),

                PONo = string.IsNullOrWhiteSpace(poNoColumn) ? "PONo" : poNoColumn.Trim()

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

        string? dueDateColumn,

        string? salesOrderColumn = null,

        string? poNoColumn = null)

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

                DueDate = string.IsNullOrWhiteSpace(dueDateColumn) ? "DueDate" : dueDateColumn.Trim(),

                SalesOrder = string.IsNullOrWhiteSpace(salesOrderColumn) ? "Sales Order" : salesOrderColumn.Trim(),

                PONo = string.IsNullOrWhiteSpace(poNoColumn) ? "PONo" : poNoColumn.Trim()

            };



            using var stream = new MemoryStream(fileBytes);

            var upload = await _previews.UploadAsync(stream, fileName, fileBytes.Length, UserName(), customMap);



            _logger.LogInformation(

                "BM pending upload {Id} ({File}) by {User}: {Rows} rows. Code='{Code}', Desc='{Desc}', SO='{SO}', PO='{PO}'",

                upload.UploadId, upload.FileName, UserName(), upload.RowCount, customMap.ProductCode, customMap.Remarks, customMap.SalesOrder, customMap.PONo);



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
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;
        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();

        var upload = await _previews.GetAsync(id);
        if (upload == null) return NotFound();

        if (!await CanAccessUploadAsync(id, currentUserCtx))
        {
            return Forbid();
        }

        BmRefreshPreviewResult? refreshPreview = null;
        if (upload.Status == BmUploadStatus.Previewed || upload.Rows.Any(r => r.ImportStatus is BmRowImportStatus.Ok or BmRowImportStatus.Warning))
        {
            try
            {
                refreshPreview = await _jobs.GetRefreshPreviewAsync(id, effectiveDeptId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not compute refresh preview for upload {Id}", id);
            }
        }
        else if (upload.Status == BmUploadStatus.Confirmed)
        {
            var absentJobs = await _jobs.GetAbsentJobsAsync(id, effectiveDeptId);
            refreshPreview = new BmRefreshPreviewResult(id, new Dictionary<int, BmRowJobMatch>(), absentJobs);
        }

        var jobsByRow = new Dictionary<int, (string JobNo, string Status)>();
        var jobsByOrder = new Dictionary<(string Code, string So, string Po), (string JobNo, string Status)>();

        if (_db != null)
        {
            var rowIds = upload.Rows.Select(r => r.RowId).ToList();
            var codes = upload.Rows.Where(r => !string.IsNullOrWhiteSpace(r.ProductCode)).Select(r => r.ProductCode.Trim().ToUpperInvariant()).Distinct().ToList();

            var relevantJobsQuery = _db.Jobs
                .Include(j => j.Product)
                .Include(j => j.AssemblyLine)
                .Where(j => (j.SourceBmRowId != null && rowIds.Contains(j.SourceBmRowId.Value)) ||
                            (!string.IsNullOrWhiteSpace(j.SalesOrder) && codes.Contains(j.Product.ProductCode.ToUpper())));

            if (!isPlantHead && effectiveDeptId.HasValue)
            {
                relevantJobsQuery = relevantJobsQuery.Where(j =>
                    (j.Product != null && j.Product.DepartmentId == effectiveDeptId.Value) ||
                    (j.AssemblyLine != null && j.AssemblyLine.DepartmentId == effectiveDeptId.Value));
            }

            var relevantJobs = await relevantJobsQuery.ToListAsync();

            foreach (var j in relevantJobs)
            {
                if (j.SourceBmRowId.HasValue && !jobsByRow.ContainsKey(j.SourceBmRowId.Value))
                    jobsByRow[j.SourceBmRowId.Value] = (j.JobNo, j.Status.ToString());

                var jCode = (j.Product?.ProductCode ?? string.Empty).Trim().ToUpperInvariant();
                var jSo = (j.SalesOrder ?? string.Empty).Trim().ToUpperInvariant();
                var jPo = (j.PONo ?? string.Empty).Trim().ToUpperInvariant();

                if (jSo.Length > 0 || jPo.Length > 0)
                {
                    var key = (jCode, jSo, jPo);
                    if (!jobsByOrder.ContainsKey(key))
                        jobsByOrder[key] = (j.JobNo, j.Status.ToString());
                }
            }
        }

        var vm = new BmPreviewViewModel
        {
            UploadId = upload.UploadId,
            FileName = upload.FileName,
            UploadedAt = upload.UploadedAt,
            UploadedBy = upload.UploadedBy,
            Status = upload.Status,
            Legend = await _colours.GetActiveAsync(),
            RefreshPreview = refreshPreview,
            Rows = upload.Rows
                .OrderBy(r => r.ExcelRowNo)
                .Select(r =>
                {
                    BmRowJobMatch? match = null;
                    refreshPreview?.MatchedRows.TryGetValue(r.RowId, out match);

                    string? createdJobNo = null;
                    string? createdJobStatus = null;
                    if (jobsByRow.TryGetValue(r.RowId, out var jInfo))
                    {
                        createdJobNo = jInfo.JobNo;
                        createdJobStatus = jInfo.Status;
                    }
                    else if (jobsByOrder.TryGetValue(((r.ProductCode ?? "").Trim().ToUpperInvariant(), (r.SalesOrder ?? "").Trim().ToUpperInvariant(), (r.PONo ?? "").Trim().ToUpperInvariant()), out var jInfo2))
                    {
                        createdJobNo = jInfo2.JobNo;
                        createdJobStatus = jInfo2.Status;
                    }

                    return new BmRowViewModel
                    {
                        RowId = r.RowId,
                        ExcelRowNo = r.ExcelRowNo,
                        ProductCode = r.ProductCode ?? string.Empty,
                        Quantity = r.Quantity,
                        DueDate = r.DueDate,
                        Remarks = r.Remarks,
                        ColourHex = r.ColourHex,
                        Priority = r.Priority,
                        ImportStatus = r.ImportStatus,
                        ImportMessage = r.ImportMessage,
                        SalesOrder = r.SalesOrder,
                        PONo = r.PONo,
                        MatchedJob = match,
                        CreatedJobNo = createdJobNo,
                        CreatedJobStatus = createdJobStatus
                    };
                }).ToList()
        };

        // A failed upload keeps no row text, so read the errors back off the file.
        if (upload.Status == BmUploadStatus.Failed)
            vm.FileErrors = await _previews.ReadFileErrorsAsync(upload);

        // Always show jobs produced from this upload if any exist
        vm.JobNumbers = (await _jobs.GetJobNumbersAsync(upload.UploadId)).ToList();



        ViewData["Title"] = $"Preview — {upload.FileName}";

        ViewData["ActiveNav"] = "Workload";

        return View(vm);

    }



    private async Task<bool> CanAccessUploadAsync(int uploadId, UserContextDto? currentUserCtx)
    {
        if (currentUserCtx == null || currentUserCtx.HasFullPlantAccess) return true;
        var effectiveDeptId = currentUserCtx.GetEffectiveDepartmentId();
        if (!effectiveDeptId.HasValue) return true;

        var upload = await _previews.GetAsync(uploadId);
        if (upload == null) return false;
        if (string.IsNullOrEmpty(upload.UploadedBy)) return true;

        var uploader = await _userManager.FindByEmailAsync(upload.UploadedBy) ?? await _userManager.FindByNameAsync(upload.UploadedBy);
        if (uploader?.DepartmentId.HasValue == true && uploader.DepartmentId.Value != effectiveDeptId.Value)
            return false;

        return true;
    }

    // POST /Bm/Confirm

    [HttpPost]

    public async Task<IActionResult> Confirm(int id)

    {
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        if (!await CanAccessUploadAsync(id, currentUserCtx))
        {
            return Forbid();
        }

        try

        {

            var result = await _jobs.ConfirmAsync(id, UserName());



            _logger.LogInformation(

                "BM upload {Id} confirmed by {User}: {Created} created, {Refreshed} refreshed ({Black} Black, notification: {Notified})",

                id, UserName(), result.JobsCreated, result.JobsRefreshed, result.BlackJobs, result.NotificationRaised);



            var msg = result.JobsRefreshed > 0

                ? $"Successfully processed: {result.JobsCreated} new job(s) created, {result.JobsRefreshed} existing open job(s) refreshed."

                : $"Imported {result.JobsCreated} new job(s).";



            if (result.AbsentJobsCount > 0)

            {

                msg += $" {result.AbsentJobsCount} open job(s) not in this workbook were left active.";

            }



            TempData["Success"] = msg;



            if (result.BlackJobs > 0 && _hubContext != null)

            {

                await _hubContext.Clients.Group("workers").SendAsync("BlackJobAlert", new

                {

                    message = $"{result.BlackJobs} urgent Black priority job(s) require immediate production!"

                });

            }

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
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        if (!await CanAccessUploadAsync(uploadId, currentUserCtx))
        {
            return Forbid();
        }

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
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        if (!await CanAccessUploadAsync(uploadId, currentUserCtx))
        {
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = "Access denied" });
            return Forbid();
        }

        try

        {

            await _previews.SetRowSkippedAsync(rowId, skipped);

            var message = skipped ? "Row skipped." : "Row restored.";

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true, message });

            TempData["Success"] = message;

        }

        catch (InvalidOperationException ex)

        {

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = false, message = ex.Message });

            TempData["Error"] = ex.Message;

        }



        return RedirectToAction(nameof(Preview), new { id = uploadId });

    }



    // POST /Bm/Cancel

    [HttpPost]

    public async Task<IActionResult> Cancel(int id)

    {
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        if (!await CanAccessUploadAsync(id, currentUserCtx))
        {
            return Forbid();
        }

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



    // GET /Bm/Workload -> Redundant, redirect to BM hub
    [HttpGet]
    public IActionResult Workload()
    {
        return RedirectToAction(nameof(Index));
    }

    // GET /Bm/ByItem

    [HttpGet]

    public async Task<IActionResult> ByItem(string? search)
    {
        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();
        var items = await _jobs.GetWorkloadByItemAsync(search, effectiveDeptId);



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
            var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
            var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();

            var count = await _jobs.AutoAssignJobsAsync(UserName(), effectiveDeptId);

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

    // POST /Bm/UpdateJobQuantity
    [HttpPost]
    public async Task<IActionResult> UpdateJobQuantity([FromBody] UpdateJobQuantityRequest request)
    {
        if (request == null || request.JobId <= 0)
        {
            return BadRequest(new { success = false, message = "Invalid request." });
        }

        if (request.Quantity < 1)
        {
            return BadRequest(new { success = false, message = "Quantity must be at least 1." });
        }

        try
        {
            if (_db == null)
            {
                return StatusCode(500, new { success = false, message = "Database context not available." });
            }

            var job = await _db.Jobs
                .Include(j => j.Units)
                .FirstOrDefaultAsync(j => j.JobId == request.JobId);

            if (job == null)
            {
                return NotFound(new { success = false, message = $"Job with ID {request.JobId} not found." });
            }

            // Check if user has access to this job's department
            var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
            var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();
            var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

            if (!isPlantHead && effectiveDeptId.HasValue)
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.ProductId == job.ProductId);
                var assemblyLine = job.AssemblyLineId.HasValue
                    ? await _db.AssemblyLines.FirstOrDefaultAsync(a => a.AssemblyLineId == job.AssemblyLineId.Value)
                    : null;

                if ((product?.DepartmentId ?? effectiveDeptId.Value) != effectiveDeptId.Value &&
                    (assemblyLine?.DepartmentId ?? effectiveDeptId.Value) != effectiveDeptId.Value)
                {
                    return Forbid();
                }
            }

            var oldQuantity = job.Quantity;
            var newQuantity = request.Quantity;

            if (oldQuantity == newQuantity)
            {
                return Json(new { success = true, quantity = job.Quantity, message = "No change in quantity." });
            }

            // Update the job quantity
            job.Quantity = newQuantity;
            job.UpdatedAt = DateTime.UtcNow;

            // Adjust JobUnits if needed
            var currentUnitCount = job.Units.Count;

            if (newQuantity > currentUnitCount)
            {
                // Add new units
                var unitsToAdd = newQuantity - currentUnitCount;
                for (int i = 0; i < unitsToAdd; i++)
                {
                    var newUnit = new JobUnit
                    {
                        JobId = job.JobId,
                        UnitIndex = currentUnitCount + i + 1,
                        TokenNo = TokenGenerator.GenerateProductUnitToken(),
                        ProductSerial = $"{job.JobNo}-U{currentUnitCount + i + 1:D3}",
                        Status = UnitAssemblyStatus.Pending
                    };
                    _db.JobUnits.Add(newUnit);
                }
            }
            else if (newQuantity < currentUnitCount)
            {
                // Remove excess units (only pending ones)
                var unitsToRemove = job.Units
                    .Where(u => u.Status == UnitAssemblyStatus.Pending)
                    .OrderByDescending(u => u.UnitIndex)
                    .Take(currentUnitCount - newQuantity)
                    .ToList();

                if (unitsToRemove.Count < (currentUnitCount - newQuantity))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cannot reduce quantity below {currentUnitCount - unitsToRemove.Count}. Some units are already in progress or completed."
                    });
                }

                _db.JobUnits.RemoveRange(unitsToRemove);
            }

            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Job {JobNo} quantity updated from {OldQty} to {NewQty} by {User}",
                job.JobNo, oldQuantity, newQuantity, UserName());

            return Json(new
            {
                success = true,
                quantity = job.Quantity,
                message = $"Job quantity updated from {oldQuantity} to {newQuantity}."
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating quantity for job {JobId}", request.JobId);
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while updating the quantity. Please try again."
            });
        }
    }

    // POST /Bm/UpdateJobQuantitiesBatch
    [HttpPost]
    public async Task<IActionResult> UpdateJobQuantitiesBatch([FromBody] UpdateJobQuantitiesBatchRequest request)
    {
        if (request == null || request.Changes == null || request.Changes.Count == 0)
        {
            return BadRequest(new { success = false, message = "No changes provided." });
        }

        // Validate all quantities first
        if (request.Changes.Any(c => c.Quantity < 1))
        {
            return BadRequest(new { success = false, message = "All quantities must be at least 1." });
        }

        try
        {
            if (_db == null)
            {
                return StatusCode(500, new { success = false, message = "Database context not available." });
            }

            var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;
            var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId();
            var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

            var jobIds = request.Changes.Select(c => c.JobId).ToList();
            var jobs = await _db.Jobs
                .Include(j => j.Units)
                .Include(j => j.Product)
                .Include(j => j.AssemblyLine)
                .Where(j => jobIds.Contains(j.JobId))
                .ToListAsync();

            if (jobs.Count != jobIds.Count)
            {
                var missingIds = jobIds.Except(jobs.Select(j => j.JobId)).ToList();
                return NotFound(new
                {
                    success = false,
                    message = $"Jobs not found: {string.Join(", ", missingIds)}"
                });
            }

            var updatedCount = 0;
            var errors = new List<string>();

            foreach (var change in request.Changes)
            {
                var job = jobs.First(j => j.JobId == change.JobId);

                // Check department access
                if (!isPlantHead && effectiveDeptId.HasValue)
                {
                    if ((job.Product?.DepartmentId ?? effectiveDeptId.Value) != effectiveDeptId.Value &&
                        (job.AssemblyLine?.DepartmentId ?? effectiveDeptId.Value) != effectiveDeptId.Value)
                    {
                        errors.Add($"Access denied for job {job.JobNo}");
                        continue;
                    }
                }

                var oldQuantity = job.Quantity;
                var newQuantity = change.Quantity;

                if (oldQuantity == newQuantity)
                {
                    continue; // No change needed
                }

                // Update job quantity
                job.Quantity = newQuantity;
                job.UpdatedAt = DateTime.UtcNow;

                // Adjust JobUnits
                var currentUnitCount = job.Units.Count;

                if (newQuantity > currentUnitCount)
                {
                    // Add new units
                    var unitsToAdd = newQuantity - currentUnitCount;
                    for (int i = 0; i < unitsToAdd; i++)
                    {
                        var newUnit = new JobUnit
                        {
                            JobId = job.JobId,
                            UnitIndex = currentUnitCount + i + 1,
                            TokenNo = TokenGenerator.GenerateProductUnitToken(),
                            ProductSerial = $"{job.JobNo}-U{currentUnitCount + i + 1:D3}",
                            Status = UnitAssemblyStatus.Pending
                        };
                        _db.JobUnits.Add(newUnit);
                    }
                }
                else if (newQuantity < currentUnitCount)
                {
                    // Remove excess units (only pending ones)
                    var unitsToRemove = job.Units
                        .Where(u => u.Status == UnitAssemblyStatus.Pending)
                        .OrderByDescending(u => u.UnitIndex)
                        .Take(currentUnitCount - newQuantity)
                        .ToList();

                    if (unitsToRemove.Count < (currentUnitCount - newQuantity))
                    {
                        errors.Add($"Cannot reduce {job.JobNo} quantity below {currentUnitCount - unitsToRemove.Count}. Some units are in progress or completed.");
                        continue;
                    }

                    _db.JobUnits.RemoveRange(unitsToRemove);
                }

                _logger.LogInformation(
                    "Job {JobNo} quantity batch updated from {OldQty} to {NewQty} by {User}",
                    job.JobNo, oldQuantity, newQuantity, UserName());

                updatedCount++;
            }

            await _db.SaveChangesAsync();

            if (errors.Count > 0 && updatedCount == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Failed to update any quantities. Errors: {string.Join("; ", errors)}"
                });
            }

            return Json(new
            {
                success = true,
                updatedCount = updatedCount,
                message = updatedCount == 1
                    ? "1 order quantity updated successfully."
                    : $"{updatedCount} order quantities updated successfully.",
                errors = errors.Count > 0 ? errors : null
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating job quantities in batch");
            return StatusCode(500, new
            {
                success = false,
                message = "An error occurred while updating quantities. Please try again."
            });
        }
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