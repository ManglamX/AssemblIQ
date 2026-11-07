using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.QrRepository;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class QrRepositoryController : Controller
{
    private readonly AppDbContext _db;
    private readonly IUserContextService _userContext;
    private readonly IHierarchyService _hierarchy;
    private readonly IQrCodeService _qrCodeService;
    private readonly ILogger<QrRepositoryController> _logger;

    public QrRepositoryController(
        AppDbContext db,
        IUserContextService userContext,
        IHierarchyService hierarchy,
        ILogger<QrRepositoryController> logger,
        IQrCodeService? qrCodeService = null)
    {
        _db = db;
        _userContext = userContext;
        _hierarchy = hierarchy;
        _logger = logger;
        _qrCodeService = qrCodeService ?? new QrCodeService();
    }

    [HttpGet]
    [Route("Admin/QrRepository")]
    [Route("QrRepository")]
    public async Task<IActionResult> Index(
        [FromQuery] string? search = null,
        [FromQuery] int? departmentId = null,
        [FromQuery] int? assemblyLineId = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        ViewData["Title"] = "PRD QR Code Repository";
        ViewData["ActiveNav"] = "QrRepository";

        var currentUserCtx = await _userContext.GetCurrentUserContextAsync(User);
        var isPlantHead = currentUserCtx?.HasFullPlantAccess ?? false;
        var effectiveDeptId = currentUserCtx?.GetEffectiveDepartmentId(departmentId);

        // Base query for JobUnits
        var query = _db.JobUnits
            .AsNoTracking()
            .Include(u => u.Job)
                .ThenInclude(j => j.Product)
                    .ThenInclude(p => p.Department)
            .Include(u => u.Job)
                .ThenInclude(j => j.AssemblyLine)
            .Include(u => u.CompletedByWorker)
            .AsQueryable();

        // Department scoping
        if (effectiveDeptId.HasValue && effectiveDeptId.Value > 0)
        {
            query = query.Where(u => u.Job.Product.DepartmentId == effectiveDeptId.Value ||
                (u.Job.AssemblyLine != null && u.Job.AssemblyLine.DepartmentId == effectiveDeptId.Value));
        }

        // Assembly Line filter
        if (assemblyLineId.HasValue && assemblyLineId.Value > 0)
        {
            query = query.Where(u => u.Job.AssemblyLineId == assemblyLineId.Value);
        }

        // Status filter
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<UnitAssemblyStatus>(status, true, out var parsedStatus))
        {
            query = query.Where(u => u.Status == parsedStatus);
        }

        // Search filter
        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim().ToLower();
            query = query.Where(u =>
                u.TokenNo.ToLower().Contains(s) ||
                u.Job.JobNo.ToLower().Contains(s) ||
                (u.ProductSerial != null && u.ProductSerial.ToLower().Contains(s)) ||
                u.Job.Product.ProductCode.ToLower().Contains(s) ||
                u.Job.Product.ProductName.ToLower().Contains(s) ||
                (u.CompletedByWorker != null && u.CompletedByWorker.FullName.ToLower().Contains(s)));
        }

        var units = await query
            .OrderByDescending(u => u.Job.JobId)
            .ThenBy(u => u.UnitIndex)
            .Take(120)
            .ToListAsync(ct);

        // Fetch sessions to determine bolt statistics
        var unitJobIds = units.Select(u => u.JobId).Distinct().ToList();
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Results)
            .Where(s => unitJobIds.Contains(s.JobId))
            .ToListAsync(ct);

        var dtoList = units.Select(u =>
        {
            var uSessions = sessions.Where(s => s.JobId == u.JobId && s.UnitNo == u.UnitIndex).ToList();
            var allResults = uSessions.SelectMany(s => s.Results).ToList();
            var passedBolts = allResults.Where(r => r.Result == StepResult.Pass).Select(r => r.StepId).Distinct().Count();
            var totalBolts = allResults.Select(r => r.StepId).Distinct().Count();
            var hasSessions = uSessions.Any();
            var isQualityPassed = u.Status == UnitAssemblyStatus.Completed || (passedBolts > 0 && passedBolts >= totalBolts);

            return new QrUnitItemDto
            {
                JobUnitId = u.JobUnitId,
                JobId = u.JobId,
                JobNo = u.Job.JobNo,
                UnitIndex = u.UnitIndex,
                TotalQuantity = u.Job.Quantity,
                TokenNo = u.TokenNo,
                ProductSerial = u.ProductSerial,
                ProductId = u.Job.ProductId,
                ProductCode = u.Job.Product.ProductCode,
                ProductName = u.Job.Product.ProductName,
                DepartmentId = u.Job.Product.DepartmentId,
                DepartmentName = u.Job.Product.Department?.Name ?? "General",
                AssemblyLineId = u.Job.AssemblyLineId,
                AssemblyLineName = u.Job.AssemblyLine?.AssemblyName ?? "Line Unassigned",
                Status = u.Status,
                HasSessions = hasSessions,
                IsQualityPassed = isQualityPassed,
                PassedBolts = passedBolts,
                TotalBolts = totalBolts,
                StartedAt = u.StartedAt,
                CompletedAt = u.CompletedAt,
                CompletedByWorkerName = u.CompletedByWorker?.FullName,
                CreatedAt = u.CreatedAt
            };
        }).ToList();

        // Calculate KPIs
        var allDeptUnitsQuery = _db.JobUnits.AsNoTracking().AsQueryable();
        if (effectiveDeptId.HasValue && effectiveDeptId.Value > 0)
        {
            allDeptUnitsQuery = allDeptUnitsQuery.Where(u => u.Job.Product.DepartmentId == effectiveDeptId.Value ||
                (u.Job.AssemblyLine != null && u.Job.AssemblyLine.DepartmentId == effectiveDeptId.Value));
        }

        var totalUnitsCount = await allDeptUnitsQuery.CountAsync(ct);
        var totalPassedCount = await allDeptUnitsQuery.CountAsync(u => u.Status == UnitAssemblyStatus.Completed, ct);
        var totalFailedCount = await allDeptUnitsQuery.CountAsync(u => u.Status == UnitAssemblyStatus.Failed, ct);
        var totalInProgressCount = await allDeptUnitsQuery.CountAsync(u => u.Status == UnitAssemblyStatus.InProgress, ct);
        var totalPendingCount = await allDeptUnitsQuery.CountAsync(u => u.Status == UnitAssemblyStatus.Pending, ct);
        var totalJobsCount = await allDeptUnitsQuery.Select(u => u.JobId).Distinct().CountAsync(ct);

        // Available dropdown lists
        var deptList = isPlantHead
            ? await _db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).ToListAsync(ct)
            : new List<Department>();

        var lineQuery = _db.AssemblyLines.AsNoTracking().Where(a => a.IsActive);
        if (effectiveDeptId.HasValue && effectiveDeptId.Value > 0)
        {
            lineQuery = lineQuery.Where(a => a.DepartmentId == effectiveDeptId.Value);
        }
        var lines = await lineQuery.OrderBy(a => a.AssemblyName).ToListAsync(ct);

        var vm = new QrRepositoryViewModel
        {
            Units = dtoList,
            TotalCount = totalUnitsCount,
            TotalPassed = totalPassedCount,
            TotalFailed = totalFailedCount,
            TotalInProgress = totalInProgressCount,
            TotalPending = totalPendingCount,
            TotalJobs = totalJobsCount,
            Search = search,
            DepartmentId = departmentId,
            AssemblyLineId = assemblyLineId,
            Status = status,
            IsPlantHead = isPlantHead,
            CurrentUserDepartmentName = currentUserCtx?.DepartmentName,
            AvailableDepartments = deptList.Select(d => new SelectListItem
            {
                Value = d.DepartmentId.ToString(),
                Text = d.Name,
                Selected = d.DepartmentId == effectiveDeptId
            }).ToList(),
            AvailableLines = lines.Select(l => new SelectListItem
            {
                Value = l.AssemblyLineId.ToString(),
                Text = l.AssemblyName,
                Selected = l.AssemblyLineId == assemblyLineId
            }).ToList()
        };

        return View("~/Views/Admin/QrRepository/Index.cshtml", vm);
    }
}
