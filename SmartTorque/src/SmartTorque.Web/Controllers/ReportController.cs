using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Extensions;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels;



namespace SmartTorque.Web.Controllers;



[Authorize(Roles = "Admin,Worker")]

public class ReportController : Controller

{

    private readonly IReportService _reportService;

    private readonly AppDbContext _db;

    private readonly IUserContextService? _userContext;

    private readonly IHierarchyService? _hierarchy;

    private readonly ILogger<ReportController> _logger;



    public ReportController(

        IReportService reportService,

        AppDbContext db,

        ILogger<ReportController> logger,

        IUserContextService? userContext = null,

        IHierarchyService? hierarchy = null)

    {

        _reportService = reportService;

        _db = db;

        _logger = logger;

        _userContext = userContext;

        _hierarchy = hierarchy;

    }



    [HttpGet]

    public IActionResult Index(SessionReportFilter filter)

    {

        return RedirectToAction(nameof(Sessions), filter);

    }



    [HttpGet]

    public async Task<IActionResult> Sessions([FromQuery] SessionReportFilter filter, CancellationToken ct)

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;

        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(filter.DepartmentId);



        if (effectiveDeptId.HasValue)

        {

            filter = filter with { DepartmentId = effectiveDeptId.Value };

        }



        var sessions = await _reportService.GetSessionReportAsync(filter, ct);
        var productUnits = await _reportService.GetProductUnitReportAsync(filter, ct);



        var workersQuery = _db.Users.AsNoTracking();

        var productsQuery = _db.Products.AsNoTracking();



        if (effectiveDeptId.HasValue)

        {

            workersQuery = workersQuery.Where(u => u.DepartmentId == effectiveDeptId.Value);

            productsQuery = productsQuery.Where(p => p.DepartmentId == effectiveDeptId.Value);

        }



        var workers = await workersQuery

            .OrderBy(u => u.FullName)

            .Select(u => new { u.Id, Name = $"{u.FullName} ({u.EmployeeNo ?? "N/A"})" })

            .ToListAsync(ct);



        var products = await productsQuery

            .OrderBy(p => p.ProductName)

            .Select(p => new { p.ProductId, Name = $"{p.ProductCode} — {p.ProductName}" })

            .ToListAsync(ct);



        ViewBag.IsPlantHead = isPlantHead;

        ViewBag.CurrentUserDepartmentName = currentUserCtx?.DepartmentName;

        ViewBag.SelectedDepartmentId = isPlantHead ? filter.DepartmentId : effectiveDeptId;

        ViewBag.AvailableDepartments = _hierarchy != null ? (await _hierarchy.GetDepartmentsAsync(ct: ct))

            .Select(d => new HierarchyItemDto(d.DepartmentId, d.Code, d.Name, d.PlantId))

            .ToList() : new List<HierarchyItemDto>();



        var devices = await _db.Devices

            .AsNoTracking()

            .OrderBy(d => d.Name)

            .Select(d => new { d.DeviceId, Name = $"{d.Name} ({d.TransmitterId})" })

            .ToListAsync(ct);



        var vm = new SessionReportViewModel

        {

            Filter = filter,
            ViewMode = HttpContext.Request.Query["view"].ToString() == "sessions" ? "sessions" : "product",
            ProductUnits = productUnits,

            Sessions = sessions,

            Workers = new SelectList(workers, "Id", "Name", filter.WorkerId),

            Products = new SelectList(products, "ProductId", "Name", filter.ProductId),

            Devices = new SelectList(devices, "DeviceId", "Name", filter.DeviceId)

        };



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> SessionDetail(long id, CancellationToken ct)

    {

        var detail = await _reportService.GetSessionDetailAsync(id, ct);

        if (detail == null)

        {

            TempData["ErrorMessage"] = $"Session #{id} not found.";

            return RedirectToAction(nameof(Sessions));

        }



        return View(detail);
    }

    [HttpGet]
    [Route("Report/ScanPrd")]
    public async Task<IActionResult> ScanPrd([FromQuery] string? token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Json(new { success = false, found = false, message = "Please provide a valid PRD Token or QR Code." });
        }

        var cleanToken = token.Trim();

        // 1. Find JobUnit by TokenNo
        var unit = await _db.JobUnits
            .AsNoTracking()
            .Include(u => u.Job)
                .ThenInclude(j => j.Product)
            .Include(u => u.Job)
                .ThenInclude(j => j.AssemblyLine)
            .Include(u => u.CompletedByWorker)
            .FirstOrDefaultAsync(u => u.TokenNo.ToLower() == cleanToken.ToLower(), ct);

        if (unit == null)
        {
            return Json(new
            {
                success = false,
                found = false,
                token = cleanToken,
                message = $"PRD unit not present. No work order or tracking record exists for token \"{cleanToken}\"."
            });
        }

        // 2. Fetch Sessions for this Job and UnitNo
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.Results)
            .Include(s => s.Worker)
            .Where(s => s.JobId == unit.JobId && s.UnitNo == unit.UnitIndex)
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(ct);

        if (sessions.Count == 0 || !sessions.Any(s => s.Results.Any() || s.Status == SmartTorque.Core.Enums.SessionStatus.InProgress))
        {
            return Json(new
            {
                success = true,
                found = true,
                hasSessions = false,
                token = unit.TokenNo,
                unitIndex = unit.UnitIndex,
                jobId = unit.JobId,
                jobNo = unit.Job.JobNo,
                productCode = unit.Job.Product.ProductCode,
                productName = unit.Job.Product.ProductName,
                assemblyLine = unit.Job.AssemblyLine?.AssemblyName ?? "Line Unassigned",
                unitStatus = unit.Status.ToString(),
                message = $"Work Order ({unit.Job.JobNo}) exists for \"{unit.Job.Product.ProductCode}\", but assembly operations have not been performed for PRD-{unit.UnitIndex} yet."
            });
        }

        // 3. Compute quality statistics from results
        var allResults = sessions.SelectMany(s => s.Results).ToList();
        var totalSteps = sessions.SelectMany(s => s.ProductComponent?.Template?.Steps ?? Enumerable.Empty<SmartTorque.Core.Entities.TemplateStep>()).Select(st => st.TemplateStepId).Distinct().Count();
        if (totalSteps == 0) totalSteps = allResults.Select(r => r.StepId).Distinct().Count();

        var passedSteps = allResults.Where(r => r.Result == SmartTorque.Core.Enums.StepResult.Pass).Select(r => r.StepId).Distinct().Count();
        var failedSteps = allResults.Where(r => r.Result == SmartTorque.Core.Enums.StepResult.Fail).Select(r => r.StepId).Distinct().Count();

        bool isInRework = unit.Status == UnitAssemblyStatus.Rework;
        bool isPassed = !isInRework && (unit.Status == UnitAssemblyStatus.Completed || (passedSteps > 0 && failedSteps == 0 && passedSteps >= totalSteps));
        bool isFailed = !isInRework && (unit.Status == UnitAssemblyStatus.Failed || failedSteps > 0);
        string overallStatus = isInRework ? $"IN REWORK (Iteration #{unit.ReworkCount})" : (isPassed ? (unit.ReworkCount > 0 ? $"Pass (Reworked {unit.ReworkCount}x)" : "Pass") : (isFailed ? "Fail" : "In Progress"));

        var workerName = sessions.FirstOrDefault()?.Worker?.FullName ?? unit.CompletedByWorker?.FullName ?? "Unknown Worker";
        var startTime = sessions.Min(s => s.StartTime);
        var endTime = sessions.Max(s => s.EndTime);
        string durationStr = "—";
        if (allResults.Any())
        {
            var minCap = allResults.Min(r => r.CapturedAt);
            var maxCap = allResults.Max(r => r.CapturedAt);
            var diff = maxCap - minCap;
            durationStr = diff.TotalMinutes >= 1 ? $"{diff.TotalMinutes:0.0} min" : $"{diff.TotalSeconds:0} sec";
        }

        var reworkHistory = await _db.JobUnitReworks
            .AsNoTracking()
            .Where(r => r.JobUnitId == unit.JobUnitId)
            .OrderByDescending(r => r.ReworkIteration)
            .Select(r => new
            {
                r.ReworkIteration,
                r.Reason,
                r.DefectCategory,
                r.InitiatedByUserName,
                initiatedAt = r.InitiatedAt.ToIstString("dd MMM yyyy, HH:mm"),
                status = r.Status.ToString(),
                completedAt = r.CompletedAt.HasValue ? r.CompletedAt.Value.ToIstString("dd MMM yyyy, HH:mm") : "In Progress",
                r.CompletedByUserName,
                r.ResolutionNotes
            })
            .ToListAsync(ct);

        return Json(new
        {
            success = true,
            found = true,
            hasSessions = true,
            token = unit.TokenNo,
            unitIndex = unit.UnitIndex,
            totalQuantity = unit.Job.Quantity,
            jobId = unit.JobId,
            jobNo = unit.Job.JobNo,
            productCode = unit.Job.Product.ProductCode,
            productName = unit.Job.Product.ProductName,
            assemblyLine = unit.Job.AssemblyLine?.AssemblyName ?? "Shop Floor",
            overallStatus,
            isPassed,
            isFailed,
            isInRework,
            reworkCount = unit.ReworkCount,
            latestReworkReason = unit.LatestReworkReason,
            reworkHistory,
            totalBolts = totalSteps,
            passedBolts = passedSteps,
            failedBolts = failedSteps,
            workerName,
            startTime = startTime.ToString("dd MMM yyyy, HH:mm"),
            endTime = endTime.HasValue ? endTime.Value.ToString("dd MMM yyyy, HH:mm") : "In Progress",
            duration = durationStr,
            traceUrl = $"/Report/Traceability?q={Uri.EscapeDataString(unit.TokenNo)}",
            pdfUrl = $"/Report/ExportProductUnitPdfByToken?token={Uri.EscapeDataString(unit.TokenNo)}",
            filterUrl = $"/Report/Sessions?Search={Uri.EscapeDataString(unit.TokenNo)}"
        });
    }

    [HttpPost("Report/TriggerRework")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TriggerRework([FromBody] TriggerUnitReworkRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request?.Token) && (request?.JobId == null || request?.UnitNo == null))
        {
            return Json(new { success = false, message = "PRD token or Job ID and Unit Number required." });
        }

        if (string.IsNullOrWhiteSpace(request?.Reason))
        {
            return Json(new { success = false, message = "Please provide a reason or issue description for rework." });
        }

        JobUnit? unit = null;
        if (!string.IsNullOrWhiteSpace(request.Token))
        {
            var cleanToken = request.Token.Trim();
            unit = await _db.JobUnits
                .Include(u => u.Job)
                    .ThenInclude(j => j.Product)
                .FirstOrDefaultAsync(u => u.TokenNo.ToLower() == cleanToken.ToLower(), ct);
        }
        else if (request.JobId.HasValue && request.UnitNo.HasValue)
        {
            unit = await _db.JobUnits
                .Include(u => u.Job)
                    .ThenInclude(j => j.Product)
                .FirstOrDefaultAsync(u => u.JobId == request.JobId.Value && u.UnitIndex == request.UnitNo.Value, ct);
        }

        if (unit == null)
        {
            return Json(new { success = false, message = "Product unit was not found." });
        }

        var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "Admin";
        var currentUserName = User.Identity?.Name ?? "Supervisor / QA";

        // Increment rework count and set status to Rework
        unit.ReworkCount++;
        unit.Status = UnitAssemblyStatus.Rework;
        unit.LatestReworkReason = request.Reason.Trim();
        unit.CompletedAt = null;
        unit.CompletedByWorkerId = null;

        var reworkLog = new JobUnitRework
        {
            JobUnitId = unit.JobUnitId,
            JobId = unit.JobId,
            UnitIndex = unit.UnitIndex,
            TokenNo = unit.TokenNo,
            ReworkIteration = unit.ReworkCount,
            Reason = request.Reason.Trim(),
            DefectCategory = !string.IsNullOrWhiteSpace(request.DefectCategory) ? request.DefectCategory.Trim() : "Quality / Rework Adjustment",
            TargetTemplateIds = request.TargetTemplateIds,
            InitiatedByUserId = currentUserId,
            InitiatedByUserName = currentUserName,
            InitiatedAt = DateTime.UtcNow,
            Status = UnitAssemblyStatus.Rework
        };
        _db.JobUnitReworks.Add(reworkLog);

        // Update Job status
        unit.Job.Status = JobStatus.Rework;
        var completedCount = await _db.JobUnits.CountAsync(u => u.JobId == unit.JobId && u.Status == UnitAssemblyStatus.Completed && u.JobUnitId != unit.JobUnitId, ct);
        unit.Job.QuantityCompleted = completedCount;
        unit.Job.UpdatedAt = DateTime.UtcNow;

        // Reset sessions for target components or all components on this unit
        var sessions = await _db.Sessions
            .Include(s => s.ProductComponent)
            .Where(s => s.JobId == unit.JobId && s.UnitNo == unit.UnitIndex)
            .ToListAsync(ct);

        var targetIds = new List<int>();
        if (!string.IsNullOrWhiteSpace(request.TargetTemplateIds))
        {
            targetIds = request.TargetTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => int.TryParse(s, out var id) ? id : 0)
                .Where(id => id > 0)
                .ToList();
        }

        foreach (var s in sessions)
        {
            if (targetIds.Count == 0 || targetIds.Contains(s.ProductComponent.TemplateId))
            {
                s.Status = SessionStatus.InProgress;
                s.FinalResult = SmartTorque.Core.Enums.SessionResult.Incomplete;
                s.CurrentStepSequence = 1;
                s.ReworkCount = unit.ReworkCount;
                s.EndTime = null;
                s.Remarks = $"Rework #{unit.ReworkCount}: {request.Reason.Trim()}";
            }
        }

        await _db.SaveChangesAsync(ct);

        return Json(new
        {
            success = true,
            message = $"Unit #{unit.UnitIndex} ({unit.TokenNo}) successfully sent to Rework #{unit.ReworkCount}.",
            reworkIteration = unit.ReworkCount,
            token = unit.TokenNo,
            jobNo = unit.Job.JobNo,
            unitIndex = unit.UnitIndex,
            redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = unit.JobId, unitNo = unit.UnitIndex })
        });
    }

    [HttpGet("Report/UnitReworkHistory")]
    public async Task<IActionResult> UnitReworkHistory([FromQuery] string token, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token)) return Json(new { success = false, message = "Token required." });
        var cleanToken = token.Trim();
        var reworks = await _db.JobUnitReworks
            .AsNoTracking()
            .Where(r => r.TokenNo.ToLower() == cleanToken.ToLower())
            .OrderByDescending(r => r.ReworkIteration)
            .Select(r => new
            {
                r.ReworkIteration,
                r.Reason,
                r.DefectCategory,
                r.InitiatedByUserName,
                initiatedAt = r.InitiatedAt.ToIstString("dd MMM yyyy, HH:mm"),
                status = r.Status.ToString(),
                completedAt = r.CompletedAt.HasValue ? r.CompletedAt.Value.ToIstString("dd MMM yyyy, HH:mm") : "In Progress",
                r.CompletedByUserName,
                r.ResolutionNotes
            })
            .ToListAsync(ct);

        return Json(new { success = true, reworks });
    }



    [HttpGet]

    public async Task<IActionResult> Traceability([FromQuery] string? q, CancellationToken ct)

    {

        var query = q ?? string.Empty;

        var report = await _reportService.GetTraceabilityReportAsync(query, ct);



        var vm = new TraceabilityReportViewModel

        {

            Query = query,

            Report = report

        };



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, int? departmentId = null, CancellationToken ct = default)

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;

        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(departmentId);



        var summary = await _reportService.GetPassFailSummaryAsync(from, to, effectiveDeptId, ct);



        var vm = new PassFailSummaryViewModel

        {

            FromDate = from,

            ToDate = to,

            Summary = summary

        };



        ViewBag.IsPlantHead = isPlantHead;

        ViewBag.CurrentUserDepartmentName = currentUserCtx?.DepartmentName;

        ViewBag.SelectedDepartmentId = isPlantHead ? departmentId : effectiveDeptId;



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> Workers([FromQuery] DateTime? from, [FromQuery] DateTime? to, int? departmentId = null, CancellationToken ct = default)

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;

        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;

        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(departmentId);



        var workerList = await _reportService.GetWorkerPerformanceReportAsync(from, to, effectiveDeptId, ct);



        var vm = new WorkerPerformanceViewModel

        {

            FromDate = from,

            ToDate = to,

            Workers = workerList

        };



        ViewBag.IsPlantHead = isPlantHead;

        ViewBag.CurrentUserDepartmentName = currentUserCtx?.DepartmentName;

        ViewBag.SelectedDepartmentId = isPlantHead ? departmentId : effectiveDeptId;



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> Devices(CancellationToken ct)

    {

        var devices = await _reportService.GetDeviceUsageReportAsync(ct);



        var vm = new DeviceUsageViewModel

        {

            Devices = devices

        };



        return View(vm);

    }



    [HttpGet]

    public async Task<IActionResult> ExportSessionsExcel([FromQuery] SessionReportFilter filter, CancellationToken ct)

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;

        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(filter.DepartmentId);

        if (effectiveDeptId.HasValue)

        {

            filter = filter with { DepartmentId = effectiveDeptId.Value };

        }

        var bytes = await _reportService.ExportSessionsToExcelAsync(filter, ct);

        var filename = $"smarttorque_sessions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);

    }



    [HttpGet]

    public async Task<IActionResult> ExportSessionsPdf([FromQuery] SessionReportFilter filter, CancellationToken ct)

    {

        var currentUserCtx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User) : null;

        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(filter.DepartmentId);

        if (effectiveDeptId.HasValue)

        {

            filter = filter with { DepartmentId = effectiveDeptId.Value };

        }

        var bytes = await _reportService.ExportSessionsToPdfAsync(filter, ct);

        var filename = $"smarttorque_sessions_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return File(bytes, "application/pdf", filename);

    }



    [HttpGet]

    public async Task<IActionResult> ExportTraceabilityExcel([FromQuery] string q, CancellationToken ct)

    {

        var bytes = await _reportService.ExportTraceabilityToExcelAsync(q, ct);

        var filename = $"smarttorque_traceability_{DateTime.UtcNow:yyyyMMdd_HHmmss}.xlsx";

        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);

    }



    [HttpGet]

    public async Task<IActionResult> ExportTraceabilityPdf([FromQuery] string q, CancellationToken ct)

    {

        var bytes = await _reportService.ExportTraceabilityToPdfAsync(q, ct);

        var filename = $"smarttorque_traceability_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";

        return File(bytes, "application/pdf", filename);

    }

    [HttpGet]
    public async Task<IActionResult> ExportProductUnitPdf(
        [FromQuery] int jobId,
        [FromQuery] int unitNo,
        [FromQuery] InspectionReportCustomization? custom,
        CancellationToken ct)
    {
        custom ??= new InspectionReportCustomization();
        var bytes = await _reportService.ExportProductUnitPdfAsync(jobId, unitNo, custom, ct);
        var filename = $"Inspection_Report_Job{jobId}_Unit{unitNo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ActionName("ExportProductUnitPdf")]
    public async Task<IActionResult> ExportProductUnitPdfPost(
        [FromQuery] int? jobId,
        [FromQuery] int? unitNo,
        [FromForm] InspectionReportCustomization? custom,
        [FromForm] int? formJobId,
        [FromForm] int? formUnitNo,
        CancellationToken ct)
    {
        int effectiveJobId = jobId ?? formJobId ?? 0;
        int effectiveUnitNo = unitNo ?? formUnitNo ?? 1;
        custom ??= new InspectionReportCustomization();
        var bytes = await _reportService.ExportProductUnitPdfAsync(effectiveJobId, effectiveUnitNo, custom, ct);
        var filename = $"Inspection_Report_Job{effectiveJobId}_Unit{effectiveUnitNo}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpGet]
    public async Task<IActionResult> ExportProductUnitPdfByToken(
        [FromQuery] string token,
        [FromQuery] InspectionReportCustomization? custom,
        CancellationToken ct)
    {
        custom ??= new InspectionReportCustomization();
        var bytes = await _reportService.ExportProductUnitPdfByTokenAsync(token, custom, ct);
        var cleanToken = (token ?? "Unit").Trim();
        var filename = $"Inspection_Report_{cleanToken}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", filename);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    [ActionName("ExportProductUnitPdfByToken")]
    public async Task<IActionResult> ExportProductUnitPdfByTokenPost(
        [FromQuery] string? token,
        [FromForm] InspectionReportCustomization? custom,
        [FromForm] string? formToken,
        CancellationToken ct)
    {
        var effectiveToken = token ?? formToken ?? string.Empty;
        custom ??= new InspectionReportCustomization();
        var bytes = await _reportService.ExportProductUnitPdfByTokenAsync(effectiveToken, custom, ct);
        var cleanToken = (effectiveToken ?? "Unit").Trim();
        var filename = $"Inspection_Report_{cleanToken}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.pdf";
        return File(bytes, "application/pdf", filename);
    }
}

public class TriggerUnitReworkRequest
{
    public string? Token { get; set; }
    public int? JobId { get; set; }
    public int? UnitNo { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? DefectCategory { get; set; }
    public string? TargetTemplateIds { get; set; }
}