using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ReportController : Controller
{
    private readonly IReportService _reportService;
    private readonly AppDbContext _db;
    private readonly IUserContextService _userContext;
    private readonly IHierarchyService _hierarchy;
    private readonly ILogger<ReportController> _logger;

    public ReportController(
        IReportService reportService,
        AppDbContext db,
        ILogger<ReportController> logger)
        : this(reportService, db, null!, null!, logger)
    {
    }

    [Microsoft.Extensions.DependencyInjection.ActivatorUtilitiesConstructor]
    public ReportController(
        IReportService reportService,
        AppDbContext db,
        IUserContextService userContext,
        IHierarchyService hierarchy,
        ILogger<ReportController> logger)
    {
        _reportService = reportService;
        _db = db;
        _userContext = userContext;
        _hierarchy = hierarchy;
        _logger = logger;
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
    public async Task<IActionResult> Summary([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var summary = await _reportService.GetPassFailSummaryAsync(from, to, ct);

        var vm = new PassFailSummaryViewModel
        {
            FromDate = from,
            ToDate = to,
            Summary = summary
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Workers([FromQuery] DateTime? from, [FromQuery] DateTime? to, CancellationToken ct)
    {
        var workerList = await _reportService.GetWorkerPerformanceReportAsync(from, to, ct);

        var vm = new WorkerPerformanceViewModel
        {
            FromDate = from,
            ToDate = to,
            Workers = workerList
        };

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
}