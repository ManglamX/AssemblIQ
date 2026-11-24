using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.SignalR;

using Microsoft.EntityFrameworkCore;

using Microsoft.Extensions.Caching.Memory;



using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Extensions;
using SmartTorque.Data;

using SmartTorque.Services;

using SmartTorque.Web.Hubs;

using SmartTorque.Web.ViewModels.Worker;



namespace SmartTorque.Web.Areas.Worker.Controllers;



[Area("Worker")]

[Authorize(Roles = "Worker,Admin")]

public class JobBoardController : Controller

{

    private readonly AppDbContext _db;

    private readonly UserManager<ApplicationUser> _userManager;

    private readonly ISessionService _sessionService;

    private readonly IHierarchyService _hierarchy;
    private readonly IHubContext<LiveReadingHub> _hubContext;
    private readonly IMemoryCache _cache;
    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, (int? TorqueDeviceId, int? VernierDeviceId)> _workerStationTools = new();

    public static void ClearWorkerStationTool(string workerId)
    {
        if (!string.IsNullOrEmpty(workerId))
        {
            _workerStationTools.TryRemove(workerId, out _);
        }
    }



    public JobBoardController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ISessionService sessionService,
        IHierarchyService hierarchy,
        IHubContext<LiveReadingHub> hubContext,
        IMemoryCache cache)
    {
        _db = db;
        _userManager = userManager;
        _sessionService = sessionService;
        _hierarchy = hierarchy;
        _hubContext = hubContext;
        _cache = cache;
    }



    private string CurrentUserId() => _userManager.GetUserId(User) ?? string.Empty;



    [HttpGet]
    public async Task<IActionResult> Index(string? filter = "my-jobs")
    {
        // Auto-release any job that remained on hold till the end of the day to the Open pool
        await _sessionService.ReleaseEndOfDayHeldJobsAsync();

        var workerId = CurrentUserId();

        var worker = await _db.Users
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == workerId);



        var activeFilter = (filter ?? "my-jobs").ToLowerInvariant();
        if (activeFilter != "open" && activeFilter != "my-jobs" && activeFilter != "pending-units")
        {
            activeFilter = "my-jobs";
        }



        // Calculate worker's real-time active workload & incomplete assigned jobs

        var incompleteAssignedJobs = await _db.Jobs

            .AsNoTracking()

            .Where(j => j.AssignedWorkerId == workerId &&

                        j.Status != JobStatus.Completed &&

                        j.Status != JobStatus.Cancelled)

            .ToListAsync();



        var activeWorkload = incompleteAssignedJobs.Sum(j => j.EstimatedWorkload);

        var incompleteAssignedCount = incompleteAssignedJobs.Count;



        // Fetch all active assembly lines for the station switcher (cached for 5 minutes)
        var deptIdKey = worker?.DepartmentId?.ToString() ?? "all";
        var activeLines = await _cache.GetOrCreateAsync($"active_lines_dept_{deptIdKey}", async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(5);
            var linesQuery = _db.AssemblyLines
                .AsNoTracking()
                .Include(a => a.WorkStations.Where(w => w.IsActive))
                .Where(a => a.IsActive);

            if (worker?.DepartmentId.HasValue == true)
            {
                linesQuery = linesQuery.Where(a => a.DepartmentId == worker.DepartmentId.Value);
            }

            return await linesQuery
                .OrderBy(a => a.AssemblyCode)
                .ToListAsync();
        }) ?? new List<AssemblyLine>();



        // Query accessible open or assigned jobs adhering to Assembly Line boundary

        var baseQuery = _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Include(j => j.AssemblyLine)
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled);

        // Strict department isolation: Worker only sees jobs belonging to their department
        if (worker?.DepartmentId.HasValue == true)
        {
            var deptId = worker.DepartmentId.Value;
            baseQuery = baseQuery.Where(j => j.Product.DepartmentId == deptId || (j.AssemblyLine != null && j.AssemblyLine.DepartmentId == deptId));
        }



        if (worker?.CurrentAssemblyLineId != null)

        {

            var lineId = worker.CurrentAssemblyLineId.Value;

            baseQuery = baseQuery.Where(j =>

                j.AssignedWorkerId == workerId ||

                (j.AssignedWorkerId == null && (j.AssemblyLineId == lineId || j.AssemblyLineId == null)));

        }

        else

        {

            // Department Mode (All Lines & Products): worker sees all jobs in department
            baseQuery = baseQuery.Where(j =>

                j.AssignedWorkerId == workerId ||

                j.AssignedWorkerId == null);

        }



        var allJobs = await baseQuery.ToListAsync();

        var jobIds = allJobs.Select(j => j.JobId).ToList();
        var allJobUnits = await _db.JobUnits
            .AsNoTracking()
            .Where(u => jobIds.Contains(u.JobId))
            .OrderBy(u => u.UnitIndex)
            .ToListAsync();

        var jobSessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Results)
            .Where(s => jobIds.Contains(s.JobId))
            .ToListAsync();

        var productIds = allJobs.Select(j => j.ProductId).Distinct().ToList();
        var productComponents = await _db.ProductComponents
            .AsNoTracking()
            .Where(pc => productIds.Contains(pc.ProductId))
            .Include(pc => pc.Template)
                .ThenInclude(t => t.Steps)
            .ToListAsync();

        var cards = allJobs.Select(j =>
        {
            var unitsForJob = allJobUnits.Where(u => u.JobId == j.JobId).ToList();
            var sessionsForJob = jobSessions.Where(s => s.JobId == j.JobId).ToList();

            var jobComponents = productComponents.Where(pc => pc.ProductId == j.ProductId).ToList();
            var jobScopedSteps = jobComponents
                .Where(c => j.IsTemplateAllowed(c.TemplateId))
                .SelectMany(c => c.Template != null ? c.Template.Steps : Enumerable.Empty<SmartTorque.Core.Entities.TemplateStep>())
                .Where(s => j.IsStepAllowed(s.TemplateStepId))
                .Select(s => s.TemplateStepId)
                .Distinct()
                .ToList();

            int jobTotalSteps = jobScopedSteps.Count > 0 
                ? jobScopedSteps.Count 
                : (jobComponents.Sum(c => c.Template != null ? c.Template.Steps.Count : 0));

            var pendingUnitDtos = unitsForJob.Select(u =>
            {
                var unitSessions = sessionsForJob.Where(s => s.UnitNo == u.UnitIndex).ToList();
                var passedSteps = unitSessions
                    .SelectMany(s => s.Results)
                    .Where(r => r.Result == StepResult.Pass && (jobScopedSteps.Count == 0 || jobScopedSteps.Contains(r.StepId)))
                    .Select(r => r.StepId)
                    .Distinct()
                    .Count();

                var hasFail = unitSessions
                    .SelectMany(s => s.Results)
                    .Any(r => r.Result == StepResult.Fail &&
                              (jobScopedSteps.Count == 0 || jobScopedSteps.Contains(r.StepId)) &&
                              !unitSessions.SelectMany(s2 => s2.Results).Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Pass));

                var isUnitCompleted = u.Status == UnitAssemblyStatus.Completed || (!hasFail && passedSteps >= jobTotalSteps && jobTotalSteps > 0);

                return new WorkerPendingUnitDto
                {
                    UnitIndex = u.UnitIndex,
                    TokenNo = u.TokenNo,
                    Status = isUnitCompleted ? UnitAssemblyStatus.Completed : u.Status,
                    CompletedSteps = isUnitCompleted ? jobTotalSteps : passedSteps,
                    TotalSteps = jobTotalSteps,
                    HasFailedStep = hasFail && !isUnitCompleted
                };
            }).ToList();

            var completedCount = pendingUnitDtos.Count(u => u.Status == UnitAssemblyStatus.Completed);
            var effectiveQuantityCompleted = Math.Max(j.QuantityCompleted, completedCount);

            return new WorkerJobCardViewModel
            {
                JobId = j.JobId,
                JobNo = j.JobNo,
                ProductCode = j.Product.ProductCode,
                ProductName = j.Product.ProductName,
                AssemblyLineId = j.AssemblyLineId,
                AssemblyCode = j.AssemblyLine?.AssemblyCode,
                AssemblyName = j.AssemblyLine?.AssemblyName,
                EstimatedWorkload = j.EstimatedWorkload,
                Priority = j.Priority,
                PriorityLabel = j.Priority.ToString(),
                PriorityHex = GetPriorityHex(j.Priority),
                Status = j.Status,
                Quantity = j.Quantity,
                ScopeType = j.ScopeType,
                ScopeSummary = j.ScopeSummary,
                QuantityCompleted = effectiveQuantityCompleted,
                DueDate = j.DueDate,
                AssignedWorkerId = j.AssignedWorkerId,
                AssignedWorkerName = j.AssignedWorker?.FullName ?? j.AssignedWorker?.UserName,
                IsAssignedToCurrentWorker = j.AssignedWorkerId == workerId,
                PendingUnits = pendingUnitDtos
            };
        }).ToList();

        var myJobsCount = cards.Count(c => c.IsAssignedToCurrentWorker);
        var openCount = cards.Count(c => c.IsOpen);
        var pendingUnitsJobCount = cards.Count(c => c.PendingUnits.Any(u => u.Status != UnitAssemblyStatus.Completed && (u.Status == UnitAssemblyStatus.InProgress || u.CompletedSteps > 0 || u.Status == UnitAssemblyStatus.Rework)));

        var filtered = activeFilter switch
        {
            "open" => cards.Where(c => c.IsOpen).ToList(),
            _ => cards.Where(c => c.IsAssignedToCurrentWorker).ToList()
        };

        var ordered = filtered
            .OrderBy(c => (activeFilter == "open" && c.PendingUnits.Any(u => u.CompletedSteps > 0 || u.Status == UnitAssemblyStatus.InProgress || u.Status == UnitAssemblyStatus.Rework)) ? 0 : 1)
            .ThenBy(c => c.Priority == PriorityLevel.Black ? 0 : 1)
            .ThenBy(c => c.Priority)
            .ThenBy(c => c.DueDate ?? DateOnly.MaxValue)
            .ThenBy(c => c.JobNo)
            .ToList();



        var torqueDevices = await _db.Devices

            .Where(d => d.InstrumentType == InstrumentType.Torque && d.IsActive)

            .OrderBy(d => d.Name)

            .ToListAsync();

        var vernierDevices = await _db.Devices
            .Where(d => d.InstrumentType == InstrumentType.Vernier && d.IsActive)
            .OrderBy(d => d.Name)
            .ToListAsync();

        ViewBag.AvailableTorqueDevices = torqueDevices;
        ViewBag.AvailableVernierDevices = vernierDevices;

        // Concurrency Lock: Query devices currently in-use by active in-progress sessions (excluding current worker)
        var activeSessions = await _db.Sessions
            .Include(s => s.Worker)
            .Include(s => s.Job)
            .Where(s => s.Status == SessionStatus.InProgress && s.EndTime == null && s.WorkerId != workerId)
            .ToListAsync();

        var deviceLocks = new Dictionary<int, string>();
        foreach (var s in activeSessions)
        {
            var workerName = !string.IsNullOrWhiteSpace(s.Worker?.FullName)
                ? s.Worker.FullName
                : (!string.IsNullOrWhiteSpace(s.Worker?.UserName) ? s.Worker.UserName : "Operator");
            var jobInfo = s.Job?.JobNo ?? $"Job #{s.JobId}";
            var lockDesc = $"{workerName} ({jobInfo})";

            if (s.TorqueDeviceId.HasValue && !deviceLocks.ContainsKey(s.TorqueDeviceId.Value))
            {
                deviceLocks[s.TorqueDeviceId.Value] = lockDesc;
            }
            if (s.VernierDeviceId.HasValue && !deviceLocks.ContainsKey(s.VernierDeviceId.Value))
            {
                deviceLocks[s.VernierDeviceId.Value] = lockDesc;
            }
        }
        ViewBag.DeviceLocks = deviceLocks;
        ViewBag.CurrentWorkerId = workerId;

        // Only populate if worker has explicitly paired/connected a tool in the current shift/session
        if (!string.IsNullOrEmpty(workerId) && _workerStationTools.TryGetValue(workerId, out var pref) && (pref.TorqueDeviceId.HasValue || pref.VernierDeviceId.HasValue))
        {
            if (pref.TorqueDeviceId.HasValue)
            {
                var dev = await _db.Devices.FindAsync(pref.TorqueDeviceId.Value);
                ViewBag.WorkerActiveTorqueDeviceId = dev?.DeviceId;
                ViewBag.WorkerActiveTorqueDeviceName = dev?.Name;
                ViewBag.WorkerActiveTorqueTransmitterId = dev?.TransmitterId;
            }
            if (pref.VernierDeviceId.HasValue)
            {
                var dev = await _db.Devices.FindAsync(pref.VernierDeviceId.Value);
                ViewBag.WorkerActiveVernierDeviceId = dev?.DeviceId;
                ViewBag.WorkerActiveVernierDeviceName = dev?.Name;
                ViewBag.WorkerActiveVernierTransmitterId = dev?.TransmitterId;
            }
        }
        else
        {
            ViewBag.WorkerActiveTorqueDeviceId = null;
            ViewBag.WorkerActiveTorqueDeviceName = null;
            ViewBag.WorkerActiveTorqueTransmitterId = null;
            ViewBag.WorkerActiveVernierDeviceId = null;
            ViewBag.WorkerActiveVernierDeviceName = null;
            ViewBag.WorkerActiveVernierTransmitterId = null;
        }

        var productsQuery = _db.Products
            .AsNoTracking()
            .Where(p => p.IsActive);

        if (worker?.DepartmentId.HasValue == true)
        {
            productsQuery = productsQuery.Where(p => p.DepartmentId == worker.DepartmentId.Value);
        }

        var availableProducts = await productsQuery
            .OrderBy(p => p.ProductCode)
            .ToListAsync();

        ViewBag.AvailableProducts = availableProducts;

        var vm = new WorkerJobBoardViewModel
        {
            WorkerName = worker?.FullName ?? worker?.UserName ?? "Worker",
            WorkerId = workerId,
            DepartmentId = worker?.DepartmentId,
            DepartmentName = worker?.Department?.Name,
            CurrentAssemblyLineId = worker?.CurrentAssemblyLineId,
            CurrentAssemblyCode = worker?.CurrentAssemblyLine?.AssemblyCode,
            CurrentAssemblyName = worker?.CurrentAssemblyLine?.AssemblyName,
            CurrentWorkStationId = worker?.WorkStationId,
            CurrentStationCode = worker?.WorkStation?.StationCode,
            CurrentStationName = worker?.WorkStation?.StationName,
            CurrentStationType = worker?.WorkStation?.StationType,
            WorkerActiveWorkload = activeWorkload,
            AvailableAssemblyLines = activeLines,
            AvailableProducts = availableProducts,
            ActiveFilter = activeFilter,

            TotalCount = cards.Count,

            MyJobsCount = myJobsCount,

            BlackCount = 0,

            OpenCount = openCount,
            PendingUnitsJobsCount = pendingUnitsJobCount,
            IncompleteAssignedJobsCount = incompleteAssignedCount,

            Jobs = ordered

        };


        return View(vm);

    }



    [HttpPost]

    [Route("Worker/JobBoard/SetAssemblyLine")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SetAssemblyLine(int? assemblyLineId, int? workStationId = null, string? returnUrl = null)

    {

        var workerId = CurrentUserId();

        var worker = await _db.Users

            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.Department)

            .FirstOrDefaultAsync(u => u.Id == workerId);



        if (worker == null) return Challenge();



        if (assemblyLineId.HasValue && assemblyLineId.Value > 0)

        {

            var line = await _db.AssemblyLines
                .FirstOrDefaultAsync(a => a.AssemblyLineId == assemblyLineId.Value && a.IsActive &&
                    (!worker.DepartmentId.HasValue || a.DepartmentId == worker.DepartmentId.Value));



            if (line == null)

            {

                TempData["Error"] = "Selected assembly line is invalid or currently inactive.";

                return RedirectToAction(nameof(Index));

            }



            worker.CurrentAssemblyLineId = line.AssemblyLineId;
            WorkStation? station = null;
            if (workStationId.HasValue && workStationId.Value > 0)
            {
                station = await _db.WorkStations.FirstOrDefaultAsync(w => w.WorkStationId == workStationId.Value && w.AssemblyLineId == line.AssemblyLineId && w.IsActive);
                worker.WorkStationId = station?.WorkStationId;
            }
            else
            {
                worker.WorkStationId = null;
            }

            if (station != null)
            {
                TempData["Success"] = $"Station switched to {line.AssemblyCode} â€” Station: {station.StationCode} ({station.StationName}).";
            }
            else
            {
                TempData["Success"] = $"Station switched to Assembly Line: {line.AssemblyCode} â€” {line.AssemblyName} (Line-Wide).";
            }

        }

        else

        {

            worker.CurrentAssemblyLineId = null;
            worker.WorkStationId = null;

            var deptName = worker.Department?.Name ?? "Department";
            TempData["Success"] = $"Shifted to Department mode: {deptName} (All lines & products accessible).";

        }



        worker.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();



        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))

            return Redirect(returnUrl);



        return RedirectToAction(nameof(Index));

    }



    [HttpPost]

    [Route("Worker/JobBoard/Start/{id}")]

    [Route("Worker/Start/{id}")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Start(int id, int? torqueDeviceId = null, int? vernierDeviceId = null, int? unitNo = null, bool allMode = false, string? returnUrl = null)

    {

        var workerId = CurrentUserId();

        var job = await _db.Jobs

            .Include(j => j.AssemblyLine)

            .FirstOrDefaultAsync(j => j.JobId == id);

        if (job == null) return NotFound();

        if (allMode)
        {
            job.ScopeType = AllocationScopeType.FullProduct;
            job.AllowedTemplateIds = null;
            job.AllowedStepIds = null;
            job.ScopeSummary = "All Mode (Full Product Assembly)";
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }



        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)

        {

            TempData["Error"] = $"Job {job.JobNo} is already {job.Status.ToString().ToLowerInvariant()}.";

            return RedirectToAction(nameof(Index));

        }

        if (torqueDeviceId.HasValue && vernierDeviceId.HasValue)
        {
            TempData["Error"] = "Please select only one instrument at a time (either Torque Wrench or Vernier Caliper).";
            return RedirectToAction(nameof(Index));
        }

        // Cache tool preference
        if (torqueDeviceId.HasValue || vernierDeviceId.HasValue)
        {
            _workerStationTools[workerId] = (torqueDeviceId, vernierDeviceId);
        }

        // Concurrency Check: Ensure tool is not currently locked and in use by another operator
        if (torqueDeviceId.HasValue || vernierDeviceId.HasValue)
        {
            var targetDevId = torqueDeviceId ?? vernierDeviceId!.Value;

            // Auto-expire stale locks older than 30 minutes
            var staleSessions = await _db.Sessions
                .Where(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId
                    && s.StartTime < DateTime.UtcNow.AddMinutes(-30))
                .ToListAsync();
            foreach (var stale in staleSessions)
            {
                stale.TorqueDeviceId = null;
                stale.VernierDeviceId = null;
                stale.Status = SessionStatus.OnHold;
            }
            if (staleSessions.Count > 0) await _db.SaveChangesAsync();

            var conflictingSession = await _db.Sessions
                .Include(s => s.Worker)
                .Include(s => s.Job)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId);

            if (conflictingSession != null)
            {
                var otherWorker = !string.IsNullOrWhiteSpace(conflictingSession.Worker?.FullName) 
                    ? conflictingSession.Worker.FullName 
                    : (!string.IsNullOrWhiteSpace(conflictingSession.Worker?.UserName) ? conflictingSession.Worker.UserName : "another operator");
                var dev = await _db.Devices.FindAsync(targetDevId);
                var errMsg = $"Tool '{(dev?.Name ?? "Selected Tool")}' is currently locked and in use by {otherWorker} on Job #{conflictingSession.Job?.JobNo ?? conflictingSession.JobId.ToString()}. Please choose an available tool.";
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["X-Assembl-Shell"] == "1")
                {
                    return Json(new { success = false, message = errMsg });
                }
                TempData["Error"] = errMsg;
                return RedirectToAction(nameof(Index));
            }
        }

        if (!torqueDeviceId.HasValue && !vernierDeviceId.HasValue)
        {
            // Check in-memory preferred station tool if explicitly equipped in current shift
            if (_workerStationTools.TryGetValue(workerId, out var pref) && (pref.TorqueDeviceId.HasValue || pref.VernierDeviceId.HasValue))
            {
                torqueDeviceId = pref.TorqueDeviceId;
                vernierDeviceId = pref.VernierDeviceId;
            }
        }

        // Hard requirement: Cannot start job until a hardware tool is explicitly connected by the worker
        if (!torqueDeviceId.HasValue && !vernierDeviceId.HasValue)
        {
            var errMsg = "No hardware tool connected. Please connect a torque wrench or caliper before starting the job.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["X-Assembl-Shell"] == "1")
            {
                return Json(new { success = false, message = errMsg, requireTool = true });
            }
            TempData["Error"] = errMsg;
            return RedirectToAction(nameof(Index));
        }

        if (job.Status == JobStatus.InProgress && job.AssignedWorkerId != null && job.AssignedWorkerId != workerId)
        {
            var errMsg = $"Job {job.JobNo} is currently in progress by another worker.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["X-Assembl-Shell"] == "1")
            {
                return Json(new { success = false, message = errMsg });
            }
            TempData["Error"] = errMsg;
            return RedirectToAction(nameof(Index));
        }

        // If job is unassigned, auto-claim and assign to this worker seamlessly
        if (string.IsNullOrWhiteSpace(job.AssignedWorkerId))
        {
            job.AssignedWorkerId = workerId;
            job.Status = JobStatus.InProgress;
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        // Assembly line validation: auto-switch worker station to this line if necessary
        var workerUser = await _db.Users.Include(u => u.CurrentAssemblyLine).FirstOrDefaultAsync(u => u.Id == workerId);
        var isDeptMode = (workerUser?.CurrentAssemblyLineId == null);

        if (job.AssemblyLineId.HasValue && !isDeptMode && workerUser?.CurrentAssemblyLineId != job.AssemblyLineId.Value)
        {
            workerUser!.CurrentAssemblyLineId = job.AssemblyLineId.Value;
            await _db.SaveChangesAsync();
        }

        if (allMode || isDeptMode)
        {
            job.ScopeType = AllocationScopeType.FullProduct;
            job.AllowedTemplateIds = null;
            job.AllowedStepIds = null;
            if (string.IsNullOrWhiteSpace(job.ScopeSummary) || job.ScopeSummary.Contains("Line Scope"))
            {
                job.ScopeSummary = isDeptMode ? "Department Mode (Full Product Assembly)" : "All Mode (Full Product Assembly)";
            }
            job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        try
        {
            await _sessionService.StartOrResumeSessionAsync(id, workerId, torqueDeviceId: torqueDeviceId, vernierDeviceId: vernierDeviceId, unitNo: unitNo);

            TempData["Success"] = $"Started job {job.JobNo}.";

            var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["X-Assembl-Shell"] == "1";
            var targetExecuteUrl = (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                ? returnUrl
                : Url.Action(nameof(Execute), new { id });

            if (isAjax)
            {
                return Json(new { success = true, executeUrl = targetExecuteUrl });
            }

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Execute), new { id });
        }

        catch (DbUpdateConcurrencyException)

        {

            TempData["Error"] = $"Job {job.JobNo} was concurrently updated by another user. Please refresh.";

            return RedirectToAction(nameof(Index));

        }

    }

    [HttpGet]
    [Route("Worker/JobBoard/GetSessionState")]
    public async Task<IActionResult> GetSessionState(long sessionId, long? lastResultId = null)
    {
        var session = await _db.Sessions
            .Include(s => s.Results)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            return Json(new { isCompleted = false, hasNewResult = false, latestResultId = 0 });
        }

        var latest = session.Results.OrderByDescending(r => r.ResultId).FirstOrDefault();
        bool hasNew = latest != null && lastResultId.HasValue && latest.ResultId > lastResultId.Value;

        return Json(new
        {
            isCompleted = session.Status == SessionStatus.Completed || session.EndTime.HasValue,
            hasNewResult = hasNew,
            latestResultId = latest?.ResultId ?? 0,
            latestResult = hasNew ? new
            {
                resultId = latest!.ResultId,
                stepId = latest.StepId,
                actualValue = latest.ActualValue,
                isStepPassed = latest.Result == SmartTorque.Core.Enums.StepResult.Pass,
                attemptNo = latest.AttemptNo,
                sessionId = session.SessionId,
                isSessionCompleted = session.Status == SessionStatus.Completed || session.EndTime.HasValue
            } : null
        });
    }

    [HttpPost]
    [Route("Worker/JobBoard/PairTool")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> PairTool(int jobId, long sessionId, int? torqueDeviceId = null, int? vernierDeviceId = null, int? unitNo = null)

    {
        var workerId = CurrentUserId();
        if (torqueDeviceId.HasValue && vernierDeviceId.HasValue)
        {
            TempData["Error"] = "Please select only one instrument at a time (either Torque Wrench or Vernier Caliper).";
            return RedirectToAction(nameof(Execute), new { id = jobId, unitNo = unitNo ?? 1 });
        }

        // Concurrency Check: Ensure tool is not currently locked and in use by another operator
        if (torqueDeviceId.HasValue || vernierDeviceId.HasValue)
        {
            var targetDevId = torqueDeviceId ?? vernierDeviceId!.Value;
            var conflictingSession = await _db.Sessions
                .Include(s => s.Worker)
                .Include(s => s.Job)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId);

            if (conflictingSession != null)
            {
                var otherWorker = !string.IsNullOrWhiteSpace(conflictingSession.Worker?.FullName) 
                    ? conflictingSession.Worker.FullName 
                    : (!string.IsNullOrWhiteSpace(conflictingSession.Worker?.UserName) ? conflictingSession.Worker.UserName : "another operator");
                var dev = await _db.Devices.FindAsync(targetDevId);
                TempData["Error"] = $"Tool '{(dev?.Name ?? "Selected Tool")}' is currently locked and in use by {otherWorker} on Job #{conflictingSession.Job?.JobNo ?? conflictingSession.JobId.ToString()}. Please choose an available tool.";
                return RedirectToAction(nameof(Execute), new { id = jobId, unitNo = unitNo ?? 1 });
            }
        }

        await _sessionService.PairDevicesAsync(sessionId, torqueDeviceId, vernierDeviceId);

        TempData["Success"] = "Hardware tool paired successfully.";

        return RedirectToAction(nameof(Execute), new { id = jobId, unitNo = unitNo ?? 1 });

    }

    [HttpGet]
    [Route("Worker/JobBoard/LookupDeviceByQr")]
    [AllowAnonymous]
    public async Task<IActionResult> LookupDeviceByQr([FromQuery] string code, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Json(new { success = false, message = "No QR code payload provided." });
        }

        var trimmed = code.Trim();

        string searchKey = trimmed;
        string? candidateTransmitter = null;

        if (trimmed.StartsWith("{") && trimmed.EndsWith("}"))
        {
            try
            {
                using var doc = System.Text.Json.JsonDocument.Parse(trimmed);
                if (doc.RootElement.TryGetProperty("apiKey", out var apiKeyProp))
                {
                    searchKey = apiKeyProp.GetString() ?? searchKey;
                }
                if (doc.RootElement.TryGetProperty("transmitterId", out var transProp))
                {
                    candidateTransmitter = transProp.GetString();
                }
            }
            catch
            {
                // Not valid JSON, continue with raw string
            }
        }

        // 1. Check ApiKeyDirectoryEntries for matching RawApiKey, ApiKeyHash, or ApiScope
        var keyEntry = await _db.ApiKeyDirectoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.RawApiKey == searchKey || k.ApiKeyHash == searchKey || k.ApiScope == searchKey, ct);

        Device? device = null;
        if (keyEntry != null)
        {
            device = await _db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.TransmitterId == keyEntry.ApiScope, ct);
        }

        // 2. Direct fallback on Devices table
        if (device == null)
        {
            var hash = DeviceService.HashApiKey(searchKey);
            device = await _db.Devices
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.TransmitterId == searchKey 
                                       || d.ApiKeyHash == hash
                                       || d.ApiKeyHash == searchKey
                                       || (candidateTransmitter != null && d.TransmitterId == candidateTransmitter), ct);
        }

        if (device == null)
        {
            return Json(new { success = false, message = $"Unrecognized hardware tool QR code: {trimmed}" });
        }

        var isOverdue = device.CalibrationDue.HasValue && device.CalibrationDue.Value < DateOnly.FromDateTime(DateTime.UtcNow);
        var isDueSoon = !isOverdue && device.CalibrationDue.HasValue && device.CalibrationDue.Value <= DateOnly.FromDateTime(DateTime.UtcNow).AddDays(30);

        return Json(new
        {
            success = true,
            deviceId = device.DeviceId,
            transmitterId = device.TransmitterId,
            name = device.Name,
            instrumentType = (int)device.InstrumentType,
            instrumentTypeName = device.InstrumentType == InstrumentType.Torque ? "Torque" : "Vernier",
            instrumentTitle = device.InstrumentType == InstrumentType.Torque ? "Torque Wrench" : "Vernier Caliper",
            isOnline = device.IsOnline,
            isActive = device.IsActive,
            isOverdue = isOverdue,
            isDueSoon = isDueSoon,
            calibrationDue = device.CalibrationDue?.ToString("yyyy-MM-dd")
        });
    }




    [HttpGet]
    public async Task<IActionResult> Execute(int id, int? unitNo = null, int? productComponentId = null, int? torqueDeviceId = null, int? vernierDeviceId = null)
    {
        var workerId = CurrentUserId();
        try
        {
            var job = await _db.Jobs.AsNoTracking().FirstOrDefaultAsync(j => j.JobId == id);
            if (job == null) return NotFound();

            if (job.Status == JobStatus.Completed)
            {
                var lastSession = await _db.Sessions
                    .Where(s => s.JobId == id)
                    .OrderByDescending(s => s.SessionId)
                    .FirstOrDefaultAsync();

                if (lastSession != null)
                {
                    return RedirectToAction(nameof(Summary), new { sessionId = lastSession.SessionId });
                }
            }

            // If explicit tool passed in query, update memory cache immediately
            if (torqueDeviceId.HasValue || vernierDeviceId.HasValue)
            {
                _workerStationTools[workerId] = (torqueDeviceId, vernierDeviceId);
            }

            if (!torqueDeviceId.HasValue && !vernierDeviceId.HasValue && !_workerStationTools.ContainsKey(workerId))
            {
                TempData["Error"] = "Please connect a hardware tool before starting the job.";
                return RedirectToAction(nameof(Index));
            }

            // Automatically sync session tool to worker's preferred station tool if selected outside
            if (_workerStationTools.TryGetValue(workerId, out var prefTool) && (prefTool.TorqueDeviceId.HasValue || prefTool.VernierDeviceId.HasValue))
            {
                var sessionsToSync = await _db.Sessions
                    .Where(s => s.JobId == id && s.WorkerId == workerId && s.Status != SessionStatus.Completed)
                    .ToListAsync();

                bool changed = false;
                foreach (var s in sessionsToSync)
                {
                    if (prefTool.TorqueDeviceId.HasValue && s.TorqueDeviceId != prefTool.TorqueDeviceId.Value)
                    {
                        s.TorqueDeviceId = prefTool.TorqueDeviceId.Value;
                        changed = true;
                    }
                    if (prefTool.VernierDeviceId.HasValue && s.VernierDeviceId != prefTool.VernierDeviceId.Value)
                    {
                        s.VernierDeviceId = prefTool.VernierDeviceId.Value;
                        changed = true;
                    }
                }

                if (changed)
                {
                    var openWindows = await _db.SessionActivityWindows
                        .Where(w => w.WorkerId == workerId && w.WindowEnd == null && w.Session.JobId == id)
                        .ToListAsync();
                    var devId = prefTool.TorqueDeviceId ?? prefTool.VernierDeviceId;
                    foreach (var w in openWindows)
                    {
                        w.DeviceId = devId;
                    }
                    await _db.SaveChangesAsync();
                }
            }

            var state = await _sessionService.GetExecutionStateAsync(id, workerId, unitNo, productComponentId);

            if (state.SessionStatus == SessionStatus.Completed || state.CurrentStep == null)
            {
                return RedirectToAction(nameof(Summary), new { sessionId = state.SessionId });
            }



            ViewBag.AvailableTorqueDevices = await _db.Devices

                .Where(d => d.InstrumentType == InstrumentType.Torque && d.IsActive)

                .OrderBy(d => d.Name)

                .ToListAsync();



            ViewBag.AvailableVernierDevices = await _db.Devices
    .Where(d => d.InstrumentType == InstrumentType.Vernier && d.IsActive)
    .OrderBy(d => d.Name)

    .ToListAsync();

            // Concurrency Lock: Query devices currently in-use by active in-progress sessions (excluding current session and current worker)
            var activeExecSessions = await _db.Sessions
                .Include(s => s.Worker)
                .Include(s => s.Job)
                .Where(s => s.Status == SessionStatus.InProgress && s.EndTime == null && s.SessionId != state.SessionId && s.WorkerId != workerId)
                .ToListAsync();

            var execDeviceLocks = new Dictionary<int, string>();
            foreach (var s in activeExecSessions)
            {
                var workerName = !string.IsNullOrWhiteSpace(s.Worker?.FullName)
                    ? s.Worker.FullName
                    : (!string.IsNullOrWhiteSpace(s.Worker?.UserName) ? s.Worker.UserName : "Operator");
                var jobInfo = s.Job?.JobNo ?? $"Job #{s.JobId}";
                var lockDesc = $"{workerName} ({jobInfo})";

                if (s.TorqueDeviceId.HasValue && !execDeviceLocks.ContainsKey(s.TorqueDeviceId.Value))
                {
                    execDeviceLocks[s.TorqueDeviceId.Value] = lockDesc;
                }
                if (s.VernierDeviceId.HasValue && !execDeviceLocks.ContainsKey(s.VernierDeviceId.Value))
                {
                    execDeviceLocks[s.VernierDeviceId.Value] = lockDesc;
                }
            }
            ViewBag.DeviceLocks = execDeviceLocks;
            ViewBag.CurrentWorkerId = workerId;




            // Paired-tool calibration notices (informational only; device use is never restricted).
            var pairedTransmitters = new List<string>();
            if (!string.IsNullOrWhiteSpace(state.TorqueDeviceTransmitterId)) pairedTransmitters.Add(state.TorqueDeviceTransmitterId.Trim());
            if (!string.IsNullOrWhiteSpace(state.VernierDeviceTransmitterId)) pairedTransmitters.Add(state.VernierDeviceTransmitterId.Trim());

            var calibrationNotices = new List<object>();
            if (pairedTransmitters.Count > 0)
            {
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                var paired = await _db.Devices
                    .Where(d => pairedTransmitters.Contains(d.TransmitterId))
                    .Select(d => new { d.Name, d.TransmitterId, d.CalibrationDue })
                    .ToListAsync();

                foreach (var d in paired)
                {
                    if (!d.CalibrationDue.HasValue) continue;
                    var due = d.CalibrationDue.Value;
                    if (due > today.AddDays(30)) continue;
                    calibrationNotices.Add(new { id = d.TransmitterId, name = d.Name, due = due.ToString("yyyy-MM-dd"), overdue = due < today });
                }
            }

            ViewBag.CalibrationNotices = calibrationNotices;
            return View(state);

        }

        catch (KeyNotFoundException)

        {

            return NotFound();

        }

    }



    [HttpGet]

    [Route("Worker/JobBoard/Summary/{sessionId:long}")]

    [Route("Worker/Summary/{sessionId:long}")]

    public async Task<IActionResult> Summary(long sessionId)

    {

        var summary = await _sessionService.GetSessionSummaryAsync(sessionId);

        if (summary == null) return NotFound();



        return View(summary);

    }



    [HttpGet]
    [Route("Worker/JobBoard/History")]
    [Route("Worker/History")]
    public async Task<IActionResult> History(string? filter = "all")
    {
        var workerId = CurrentUserId();
        var worker = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == workerId);

        var history = await _sessionService.GetWorkerHistoryAsync(workerId, filter);
        ViewData["ActiveFilter"] = filter ?? "all";

        var linesQuery = _db.AssemblyLines
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (worker?.DepartmentId.HasValue == true)
        {
            linesQuery = linesQuery.Where(a => a.DepartmentId == worker.DepartmentId.Value);
        }

        ViewBag.AvailableAssemblyLines = await linesQuery
            .OrderBy(a => a.AssemblyCode)
            .ToListAsync();

        return View(history);
    }



    [HttpGet]

    [Route("Worker/JobBoard/Profile")]

    [Route("Worker/Profile")]

    public async Task<IActionResult> Profile()
    {
        var workerId = CurrentUserId();
        var stats = await _sessionService.GetWorkerProfileStatsAsync(workerId);
        var user = await _db.Users
            .Include(u => u.Department)
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .FirstOrDefaultAsync(u => u.Id == workerId);
        ViewBag.WorkerUser = user;

        if (user?.DepartmentId.HasValue == true)
        {
            ViewBag.AvailableUnits = (await _hierarchy.GetProductionUnitsAsync(user.DepartmentId.Value)).ToList();
        }
        else
        {
            ViewBag.AvailableUnits = (await _hierarchy.GetProductionUnitsAsync()).ToList();
        }

        if (user?.CurrentAssemblyLineId.HasValue == true)
        {
            ViewBag.AvailableStations = (await _hierarchy.GetWorkStationsAsync(user.CurrentAssemblyLineId.Value)).ToList();
        }
        else
        {
            ViewBag.AvailableStations = new List<WorkStation>();
        }

        return View(stats);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Worker/Profile/UpdatePlacement")]
    public async Task<IActionResult> UpdatePlacement(int? assemblyLineId, int? workStationId)
    {
        var workerId = CurrentUserId();
        var user = await _userManager.FindByIdAsync(workerId);
        if (user != null)
        {
            user.CurrentAssemblyLineId = (assemblyLineId > 0) ? assemblyLineId : null;
            user.WorkStationId = (workStationId > 0) ? workStationId : null;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = "Shop-floor Line and Station placement updated successfully.";
        }
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    [Route("Worker/Profile/ChartData")]
    [Route("Worker/JobBoard/Profile/ChartData")]
    public async Task<IActionResult> ProfileChartData()
    {
        var workerId = CurrentUserId();
        var charts = await _sessionService.GetWorkerProfileChartsAsync(workerId);
        return Json(charts);
    }

    [HttpGet]
    [Route("Worker/Profile/GetStationsForLine")]
    [Route("Worker/JobBoard/GetStationsForLine")]
    public async Task<IActionResult> GetStationsForLine(int lineId)
    {
        var cacheKey = $"stations_line_worker_{lineId}";
        var result = await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.SlidingExpiration = TimeSpan.FromMinutes(10);
            var stations = await _hierarchy.GetWorkStationsAsync(lineId);
            return stations.Select(s => new
            {
                id = s.WorkStationId,
                workStationId = s.WorkStationId,
                code = s.StationCode,
                stationCode = s.StationCode,
                name = $"{s.StationName} ({(s.StationType == StationType.Subcell ? "Subcell" : "Station")})",
                stationName = s.StationName,
                stationType = s.StationType.ToString()
            }).ToList();
        });

        return Json(result);
    }



    [HttpGet]
    [ActionName("Units")]
    [Route("Worker/JobBoard/Units/{id}")]
    [Route("Worker/JobBoard/GetUnits/{id}")]
    public async Task<IActionResult> GetUnits(int id)
    {
        var units = await _sessionService.GetJobUnitsAsync(id);
        return Json(units.Select(u => new
        {
            u.JobUnitId,
            u.UnitIndex,
            u.TokenNo,
            Status = u.Status.ToString(),
            u.ReworkCount,
            u.LatestReworkReason
        }));
    }



    public class SwitchComponentAjaxRequest
    {
        public int JobId { get; set; }
        public int ProductComponentId { get; set; }
        public int? UnitNo { get; set; }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SwitchComponentAjax([FromBody] SwitchComponentAjaxRequest req)
    {
        var workerId = CurrentUserId();
        var session = await _sessionService.SwitchComponentAsync(req.JobId, req.ProductComponentId, workerId, req.UnitNo);
        var state = await _sessionService.GetExecutionStateAsync(req.JobId, workerId, session.UnitNo, session.ProductComponentId);

        var currentStep = state.CurrentStep;

        return Json(new
        {
            success = true,
            sessionId = state.SessionId,
            jobId = state.JobId,
            unitNo = state.UnitNo,
            componentId = state.CurrentComponent.ProductComponentId,
            componentName = state.CurrentComponent.ComponentName,
            assemblyOrder = state.CurrentComponent.AssemblyOrder,
            templateCode = state.CurrentComponent.TemplateCode,
            stepCount = state.CurrentComponent.StepCount,
            imagePath = state.CurrentComponent.ImagePath,
            currentStepSequence = currentStep?.SequenceNo ?? 1,
            target = currentStep?.Target ?? 0,
            lowerLimit = currentStep?.LowerLimit ?? 0,
            upperLimit = currentStep?.UpperLimit ?? 0,
            unitSymbol = currentStep?.Instrument == InstrumentType.Torque ? "NÂ·m" : "mm",
            stepLabel = currentStep?.Label ?? "",
            instrumentType = currentStep != null ? (int)currentStep.Instrument : 1,
            steps = state.Steps.Select(s => new
            {
                stepId = s.StepId,
                sequenceNo = s.SequenceNo,
                label = s.Label,
                instrument = (int)s.Instrument,
                target = s.Target,
                lowerLimit = s.LowerLimit,
                upperLimit = s.UpperLimit,
                markerX = s.MarkerX,
                markerY = s.MarkerY,
                markerSize = s.MarkerSize,
                isCompleted = s.IsCompleted,
                isCurrent = s.IsCurrent,
                attemptCount = s.AttemptCount
            }),
            components = state.Components.Select(c => new
            {
                productComponentId = c.ProductComponentId,
                componentName = c.ComponentName,
                assemblyOrder = c.AssemblyOrder,
                stepCount = c.StepCount,
                isDone = c.IsCompletedForCurrentUnit,
                isCurrent = c.ProductComponentId == state.CurrentComponent.ProductComponentId
            })
        });
    }

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SwitchComponent(int id, int productComponentId, int? unitNo = null)

    {

        var workerId = CurrentUserId();

        var session = await _sessionService.SwitchComponentAsync(id, productComponentId, workerId, unitNo);

        return RedirectToAction(nameof(Execute), new { id, unitNo = session.UnitNo, productComponentId = session.ProductComponentId });

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SwitchUnit(int id, int unitNo, int? productComponentId = null)
    {
        var workerId = CurrentUserId();
        var isAjax = Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["X-Assembl-Shell"] == "1";

        var targetUnit = await _db.JobUnits.FirstOrDefaultAsync(u => u.JobId == id && u.UnitIndex == unitNo);
        if (targetUnit != null && targetUnit.Status == UnitAssemblyStatus.Completed && targetUnit.Status != UnitAssemblyStatus.Rework)
        {
            var completedSess = await _db.Sessions
                .Where(s => s.JobId == id && s.UnitNo == unitNo && s.Status == SessionStatus.Completed)
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync();

            if (completedSess != null)
            {
                var sumUrl = Url.Action(nameof(Summary), new { sessionId = completedSess.SessionId });
                if (isAjax)
                {
                    return Json(new { success = true, executeUrl = sumUrl });
                }
                return RedirectToAction(nameof(Summary), new { sessionId = completedSess.SessionId });
            }
        }

        var session = await _sessionService.SwitchUnitAsync(id, unitNo, workerId, productComponentId);

        if (_workerStationTools.TryGetValue(workerId, out var pref) && (pref.TorqueDeviceId.HasValue || pref.VernierDeviceId.HasValue))
        {
            if (pref.TorqueDeviceId.HasValue) session.TorqueDeviceId = pref.TorqueDeviceId.Value;
            if (pref.VernierDeviceId.HasValue) session.VernierDeviceId = pref.VernierDeviceId.Value;
            await _db.SaveChangesAsync();
        }

        var execUrl = Url.Action(nameof(Execute), new { id, unitNo = session.UnitNo, productComponentId = session.ProductComponentId });
        if (isAjax)
        {
            return Json(new { success = true, executeUrl = execUrl });
        }
        return RedirectToAction(nameof(Execute), new { id, unitNo = session.UnitNo, productComponentId = session.ProductComponentId });
    }



    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Hold(int id, string? remarks)
    {
        var workerId = CurrentUserId();
        var activeSession = await _sessionService.GetActiveSessionForJobAsync(id, workerId);
        if (activeSession != null)
        {
            await _sessionService.HoldSessionAsync(activeSession.SessionId, remarks);
            TempData["Success"] = "Job placed on hold.";
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Worker/JobBoard/HoldSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> HoldSession(int jobId, long? sessionId = null, int? unitNo = null, string? reason = null)
    {
        var workerId = CurrentUserId();
        Session? session = null;
        if (sessionId.HasValue && sessionId.Value > 0)
        {
            session = await _db.Sessions.Include(s => s.Job).FirstOrDefaultAsync(s => s.SessionId == sessionId.Value && s.WorkerId == workerId);
        }
        if (session == null)
        {
            session = await _sessionService.GetActiveSessionForJobAsync(jobId, workerId, unitNo);
        }

        if (session == null)
        {
            return Json(new { success = false, message = "Active session not found." });
        }

        var holdRemark = string.IsNullOrWhiteSpace(reason) ? "Operator paused work" : reason.Trim();
        await _sessionService.HoldSessionAsync(session.SessionId, holdRemark);

        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("SessionHoldChanged", new
        {
            sessionId = session.SessionId,
            isOnHold = true,
            reason = holdRemark,
            accumulatedSeconds = session.AccumulatedActiveSeconds
        });

        return Json(new
        {
            success = true,
            isOnHold = true,
            accumulatedSeconds = session.AccumulatedActiveSeconds,
            message = "Job placed on hold. Timer is paused."
        });
    }

    [HttpPost]
    [Route("Worker/JobBoard/ResumeSession")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResumeSession(int jobId, long? sessionId = null, int? unitNo = null)
    {
        var workerId = CurrentUserId();
        Session? session = null;
        if (sessionId.HasValue && sessionId.Value > 0)
        {
            session = await _db.Sessions.Include(s => s.Job).FirstOrDefaultAsync(s => s.SessionId == sessionId.Value && s.WorkerId == workerId);
        }
        if (session == null)
        {
            session = await _sessionService.GetActiveSessionForJobAsync(jobId, workerId, unitNo);
        }

        if (session == null)
        {
            return Json(new { success = false, message = "Active session not found." });
        }

        await _sessionService.ResumeSessionAsync(session.SessionId);

        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("SessionHoldChanged", new
        {
            sessionId = session.SessionId,
            isOnHold = false,
            accumulatedSeconds = session.AccumulatedActiveSeconds
        });

        return Json(new
        {
            success = true,
            isOnHold = false,
            accumulatedSeconds = session.AccumulatedActiveSeconds,
            message = "Job resumed successfully. Timer is running."
        });
    }

    [HttpPost]
    [Route("Worker/JobBoard/ReleaseToOpenPool")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseToOpenPool(int jobId, long? sessionId = null)
    {
        var success = await _sessionService.ReleaseJobToOpenPoolAsync(jobId, sessionId);
        if (success)
        {
            TempData["Success"] = "Job unassigned and moved to Open Work pool.";
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
            {
                return Json(new { success = true, redirectUrl = Url.Action(nameof(Index)) });
            }
            return RedirectToAction(nameof(Index));
        }

        if (Request.Headers["X-Requested-With"] == "XMLHttpRequest" || Request.Headers["Accept"].ToString().Contains("application/json"))
        {
            return Json(new { success = false, message = "Failed to release job to open pool." });
        }
        TempData["Error"] = "Failed to release job.";
        return RedirectToAction(nameof(Index));
    }



    [HttpPost]

    [Route("Worker/JobBoard/SubmitReading")]

    [IgnoreAntiforgeryToken]

    public async Task<IActionResult> SubmitReading([FromBody] ProcessReadingCommand command)

    {

        var result = await _sessionService.ProcessReadingAsync(command);



        if (result.Success)

        {

            // Broadcast via SignalR to connected clients on this session

            await _hubContext.Clients.Group($"session-{result.SessionId}").SendAsync("ReadingProcessed", result);



            if (result.IsSessionCompleted)

            {

                await _hubContext.Clients.Group($"session-{result.SessionId}").SendAsync("SessionCompleted", result);

            }

        }



        return Json(result);

    }

    [HttpGet]
    [Route("Worker/JobBoard/Simulator")]
    [AllowAnonymous]
    public async Task<IActionResult> Simulator(long? sessionId = null)
    {
        var activeSessions = await _db.Sessions
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.Worker)
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
            .Where(s => s.Status == SessionStatus.InProgress && s.EndTime == null)
            .OrderByDescending(s => s.SessionId)
            .ToListAsync();

        Session? currentSession = null;
        if (sessionId.HasValue)
        {
            currentSession = activeSessions.FirstOrDefault(s => s.SessionId == sessionId.Value);
        }
        if (currentSession == null)
        {
            currentSession = activeSessions.FirstOrDefault();
        }

        ViewBag.ActiveSessions = activeSessions;
        return View(currentSession);
    }



    private static string GetPriorityHex(PriorityLevel priority) => priority switch

    {

        PriorityLevel.Black => "#000000",

        PriorityLevel.Red => "#FF0000",

        PriorityLevel.Yellow => "#FFD700",

        PriorityLevel.Green => "#00B050",

        PriorityLevel.Blue => "#0070C0",

        PriorityLevel.White => "#EEEEEE",

        _ => "#CCCCCC"

    };


    public class ScanPrdRequest
    {
        public string? Token { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ScanPrdUnit([FromBody] ScanPrdRequest request)
    {
        if (string.IsNullOrWhiteSpace(request?.Token))
        {
            return Json(new { success = false, message = "Please scan or enter a valid PRD Unit token." });
        }

        var token = request.Token.Trim();
        var workerId = CurrentUserId();
        var workerUser = await _db.Users.FindAsync(workerId);

        // 1. Exact match on full PRD token
        var unit = await _db.JobUnits
            .Include(u => u.Job)
                .ThenInclude(j => j.Product)
                    .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                        .ThenInclude(c => c.Template)
                            .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .Include(u => u.Job)
                .ThenInclude(j => j.AssemblyLine)
            .FirstOrDefaultAsync(u => u.TokenNo == token);

        // 2. Short Token / Damaged Label Search (last 4 to 8 characters, or unit number e.g. #04, 4F9A)
        if (unit == null)
        {
            var cleanToken = token.TrimStart('#').Trim();
            if (cleanToken.StartsWith("unit", StringComparison.OrdinalIgnoreCase))
            {
                cleanToken = cleanToken.Substring(4).Trim();
            }

            var deptId = workerUser?.DepartmentId;

            // If worker typed a unit number (e.g. "4", "#04", "04")
            var normalizedUnitNumber = cleanToken.TrimStart('0');
            if (int.TryParse(string.IsNullOrEmpty(normalizedUnitNumber) ? cleanToken : normalizedUnitNumber, out var unitIdx))
            {
                unit = await _db.JobUnits
                    .Include(u => u.Job)
                        .ThenInclude(j => j.Product)
                            .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                                .ThenInclude(c => c.Template)
                                    .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
                    .Include(u => u.Job)
                        .ThenInclude(j => j.AssemblyLine)
                    .Where(u => u.UnitIndex == unitIdx &&
                               (u.Job.AssignedWorkerId == workerId || (deptId.HasValue && u.Job.Product.DepartmentId == deptId.Value)))
                    .OrderByDescending(u => u.Job.CreatedAt)
                    .FirstOrDefaultAsync();
            }

            // Search by ending digits or substring (e.g. "3508", "4F9A", or serial)
            if (unit == null && cleanToken.Length >= 2)
            {
                var lowerToken = cleanToken.ToLowerInvariant();
                unit = await _db.JobUnits
                    .Include(u => u.Job)
                        .ThenInclude(j => j.Product)
                            .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                                .ThenInclude(c => c.Template)
                                    .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
                    .Include(u => u.Job)
                        .ThenInclude(j => j.AssemblyLine)
                    .Where(u => u.TokenNo.ToLower().EndsWith(lowerToken) ||
                                u.TokenNo.ToLower().Contains(lowerToken) ||
                                (u.ProductSerial != null && u.ProductSerial.ToLower().Contains(lowerToken)) ||
                                (u.Job.ProductSerial != null && u.Job.ProductSerial.ToLower().Contains(lowerToken)))
                    .OrderByDescending(u => u.Job.CreatedAt)
                    .FirstOrDefaultAsync();
            }
        }

        if (unit == null)
        {
            return Json(new { success = false, message = $"PRD unit matching '{token}' was not found. Try entering the last 4â€“6 characters (e.g. 4F9A) or Unit # (e.g. #04)." });
        }

        var worker = await _db.Users
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .FirstOrDefaultAsync(u => u.Id == workerId);

        var currentLineId = worker?.CurrentAssemblyLineId;
        var currentStationId = worker?.WorkStationId;
        var isDeptMode = !currentLineId.HasValue;

        // Check if there is a ProductAssemblyMapping for this Product on Worker's current line / workstation
        ProductAssemblyMapping? mapping = null;
        if (currentLineId.HasValue)
        {
            if (currentStationId.HasValue)
            {
                // First check if this specific workstation/subcell has a mapped workload for this product
                mapping = await _db.ProductAssemblyMappings
                    .FirstOrDefaultAsync(m => m.ProductId == unit.Job.ProductId && m.AssemblyLineId == currentLineId.Value && m.WorkStationId == currentStationId.Value);
            }

            // Fallback to line-wide mapping if worker is not stationed or no station-specific mapping exists
            if (mapping == null)
            {
                mapping = await _db.ProductAssemblyMappings
                    .FirstOrDefaultAsync(m => m.ProductId == unit.Job.ProductId && m.AssemblyLineId == currentLineId.Value && m.WorkStationId == null);
            }
        }

        var allowedTemplateList = (mapping != null && !string.IsNullOrWhiteSpace(mapping.AllowedTemplateIds))
            ? mapping.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList()
            : new List<int>();

        var allowedStepList = (mapping != null && !string.IsNullOrWhiteSpace(mapping.AllowedStepIds))
            ? mapping.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList()
            : new List<int>();

        // Fetch all distinct steps passed in SessionResults for this unit
        var passedStepIds = await _db.SessionResults
            .Where(r => r.Session.JobId == unit.JobId &&
                        r.Session.UnitNo == unit.UnitIndex &&
                        r.Result == StepResult.Pass)
            .Select(r => r.StepId)
            .Distinct()
            .ToListAsync();

        var allProductSteps = unit.Job.Product.Components
            .OrderBy(c => c.AssemblyOrder)
            .SelectMany(c => c.Template.Steps)
            .ToList();

        bool isFullyBuilt = allProductSteps.Count > 0 && allProductSteps.All(s => passedStepIds.Contains(s.TemplateStepId));
        int passedCount = allProductSteps.Count(s => passedStepIds.Contains(s.TemplateStepId));
        int totalCount = allProductSteps.Count;
        bool isPartiallyCompleted = passedCount > 0 && !isFullyBuilt;

        // If unit was marked Completed in DB but actually has remaining steps, self-heal DB status
        if (!isFullyBuilt && unit.Status == UnitAssemblyStatus.Completed)
        {
            unit.Status = UnitAssemblyStatus.InProgress;
            unit.CompletedAt = null;
            unit.CompletedByWorkerId = null;
            await _db.SaveChangesAsync();
        }

        // Return ALL components and steps of the product so the worker has an unrestricted view and can select any bolts.
        // Each step and template is tagged with station scope and pass/remaining status.
        var allComponents = unit.Job.Product.Components.OrderBy(c => c.AssemblyOrder).ToList();

        var components = allComponents.Select(c =>
        {
            var isCompInStationScope = isDeptMode || allowedTemplateList.Count == 0 || allowedTemplateList.Contains(c.TemplateId);
            return new
            {
                productComponentId = c.ProductComponentId,
                templateId = c.TemplateId,
                templateCode = c.Template.TemplateCode,
                templateName = c.Template.Name,
                assemblyOrder = c.AssemblyOrder,
                totalStepCount = c.Template.Steps.Count,
                stepCount = c.Template.Steps.Count,
                passedStepCount = c.Template.Steps.Count(s => passedStepIds.Contains(s.TemplateStepId)),
                remainingStepCount = c.Template.Steps.Count(s => !passedStepIds.Contains(s.TemplateStepId)),
                isStationScope = isCompInStationScope,
                steps = c.Template.Steps
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new
                    {
                        stepId = s.TemplateStepId,
                        sequenceNo = s.SequenceNo,
                        label = s.Label,
                        target = s.Target,
                        toolType = s.Instrument.ToString(),
                        isPassed = passedStepIds.Contains(s.TemplateStepId),
                        isRemaining = !passedStepIds.Contains(s.TemplateStepId),
                        isStationScope = isDeptMode || (isCompInStationScope && (allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)))
                    }).ToList()
            };
        }).ToList();

        int stationTotalCount = allProductSteps.Count(s => isDeptMode || (allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)));
        int stationPassedCount = allProductSteps.Count(s => passedStepIds.Contains(s.TemplateStepId) && (isDeptMode || allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)));
        int stationRemainingCount = allProductSteps.Count(s => !passedStepIds.Contains(s.TemplateStepId) && (isDeptMode || allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)));

        var fullStationTitle = worker?.WorkStation != null
            ? $"{worker.CurrentAssemblyLine?.AssemblyName} âž” Station: {worker.WorkStation.StationCode}"
            : (worker?.CurrentAssemblyLine?.AssemblyName ?? "Entire Department");

        var stationSummary = mapping?.ScopeSummary ?? (worker?.WorkStation != null ? $"Station {worker.WorkStation.StationCode} Scope" : (mapping != null ? "Line Workflow" : null));

        // Retrieve worker's active or last-used tools for pre-selection
        var activeSession = await _sessionService.GetActiveSessionForJobAsync(unit.JobId, workerId);
        int? activeTorqueId = activeSession?.TorqueDeviceId;
        int? activeVernierId = activeSession?.VernierDeviceId;
        if (!activeTorqueId.HasValue && !activeVernierId.HasValue)
        {
            var lastSession = await _db.Sessions
                .Where(s => s.WorkerId == workerId && (s.TorqueDeviceId != null || s.VernierDeviceId != null))
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync();
            activeTorqueId = lastSession?.TorqueDeviceId;
            activeVernierId = lastSession?.VernierDeviceId;
        }

        // If unit is truly completed with ALL product steps passed and not currently in rework: inform worker and allow triggering rework!
        if (unit.Status == UnitAssemblyStatus.Completed && isFullyBuilt)
        {
            return Json(new
            {
                success = true,
                action = "completed_prompt_rework",
                jobId = unit.JobId,
                unitIndex = unit.UnitIndex,
                tokenNo = unit.TokenNo,
                productCode = unit.Job.Product.ProductCode,
                productName = unit.Job.Product.ProductName,
                currentLineName = fullStationTitle,
                currentLineCode = worker?.CurrentAssemblyLine?.AssemblyCode ?? "",
                currentStationCode = worker?.WorkStation?.StationCode,
                currentStationName = worker?.WorkStation?.StationName,
                isStationMode = currentStationId.HasValue,
                isDeptMode = isDeptMode,
                isMapped = mapping != null,
                mappedScopeType = (int)(mapping?.ScopeType ?? AllocationScopeType.FullProduct),
                mappedScopeSummary = stationSummary,
                mappedTemplateIds = mapping?.AllowedTemplateIds,
                mappedStepIds = mapping?.AllowedStepIds,
                stationTotalStepCount = stationTotalCount,
                stationPassedStepCount = stationPassedCount,
                stationRemainingStepCount = stationRemainingCount,
                components = components,
                reworkCount = unit.ReworkCount,
                completedAt = unit.CompletedAt.HasValue ? unit.CompletedAt.Value.ToIstString("dd MMM yyyy, HH:mm") : null,
                activeTorqueDeviceId = activeTorqueId,
                activeVernierDeviceId = activeVernierId,
                message = $"PRD Unit #{unit.UnitIndex} ({unit.TokenNo}) has already PASSED quality inspection."
            });
        }

        return Json(new
        {
            success = true,
            action = "prompt_workload",
            jobId = unit.JobId,
            unitIndex = unit.UnitIndex,
            tokenNo = unit.TokenNo,
            productCode = unit.Job.Product.ProductCode,
            productName = unit.Job.Product.ProductName,
            currentLineName = fullStationTitle,
            currentLineCode = worker?.CurrentAssemblyLine?.AssemblyCode ?? "",
            currentStationCode = worker?.WorkStation?.StationCode,
            currentStationName = worker?.WorkStation?.StationName,
            isStationMode = currentStationId.HasValue,
            isDeptMode = isDeptMode,
            isMapped = mapping != null,
            mappedScopeType = (int)(mapping?.ScopeType ?? AllocationScopeType.FullProduct),
            mappedScopeSummary = stationSummary,
            mappedTemplateIds = mapping?.AllowedTemplateIds,
            mappedStepIds = mapping?.AllowedStepIds,
            isPartiallyCompleted = isPartiallyCompleted,
            passedStepCount = passedCount,
            totalStepCount = totalCount,
            remainingStepCount = totalCount - passedCount,
            stationTotalStepCount = stationTotalCount,
            stationPassedStepCount = stationPassedCount,
            stationRemainingStepCount = stationRemainingCount,
            components = components,
            activeTorqueDeviceId = activeTorqueId,
            activeVernierDeviceId = activeVernierId
        });
    }

    public class ApplyCustomWorkloadRequest
    {
        public int JobId { get; set; }
        public int UnitIndex { get; set; }
        public int ScopeType { get; set; } // 1 = Entire Product (All Mode), 2 = Specific Templates, 3 = Specific Steps
        public string? AllowedTemplateIds { get; set; }
        public string? AllowedStepIds { get; set; }
        public string? ScopeSummary { get; set; }
        public bool IsAllMode { get; set; }
        public bool IsRework { get; set; }
        public string? ReworkReason { get; set; }
        public int? TorqueDeviceId { get; set; }
        public int? VernierDeviceId { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyCustomWorkload([FromBody] ApplyCustomWorkloadRequest request)
    {
        var job = await _db.Jobs
            .Include(j => j.Product)
            .FirstOrDefaultAsync(j => j.JobId == request.JobId);

        if (job == null) return Json(new { success = false, message = "Job not found." });

        var workerId = CurrentUserId();
        var worker = await _db.Users.FirstOrDefaultAsync(u => u.Id == workerId);

        JobUnit? unit = null;
        if (request.IsRework)
        {
            unit = await _db.JobUnits
                .Include(u => u.Job)
                .FirstOrDefaultAsync(u => u.JobId == request.JobId && u.UnitIndex == request.UnitIndex);

            if (unit != null)
            {
                unit.ReworkCount++;
                unit.Status = UnitAssemblyStatus.Rework;
                unit.LatestReworkReason = !string.IsNullOrWhiteSpace(request.ReworkReason)
                    ? request.ReworkReason.Trim()
                    : "Worker initiated rework";
                unit.CompletedAt = null;
                unit.CompletedByWorkerId = null;

                var reworkLog = new JobUnitRework
                {
                    JobUnitId = unit.JobUnitId,
                    JobId = unit.JobId,
                    UnitIndex = unit.UnitIndex,
                    TokenNo = unit.TokenNo,
                    ReworkIteration = unit.ReworkCount,
                    Reason = unit.LatestReworkReason,
                    DefectCategory = "Quality / Rework Adjustment",
                    TargetTemplateIds = request.AllowedTemplateIds,
                    InitiatedByUserId = workerId,
                    InitiatedByUserName = worker?.FullName ?? "Worker",
                    InitiatedAt = DateTime.UtcNow,
                    Status = UnitAssemblyStatus.Rework
                };
                _db.JobUnitReworks.Add(reworkLog);

                // Update job status
                job.Status = JobStatus.Rework;
                var completedCount = await _db.JobUnits.CountAsync(u => u.JobId == unit.JobId && u.Status == UnitAssemblyStatus.Completed && u.JobUnitId != unit.JobUnitId);
                job.QuantityCompleted = completedCount;

                // Reset target sessions for this unit
                var sessions = await _db.Sessions
                    .Include(s => s.ProductComponent)
                    .Where(s => s.JobId == unit.JobId && s.UnitNo == unit.UnitIndex)
                    .ToListAsync();

                var targetTemplateIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(request.AllowedTemplateIds))
                {
                    targetTemplateIds = request.AllowedTemplateIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                }

                var targetStepIds = new List<int>();
                if (!string.IsNullOrWhiteSpace(request.AllowedStepIds))
                {
                    targetStepIds = request.AllowedStepIds
                        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(s => int.TryParse(s, out var id) ? id : 0)
                        .Where(id => id > 0)
                        .ToList();
                }

                foreach (var s in sessions)
                {
                    bool isTarget = false;
                    if (request.ScopeType == 1 || request.IsAllMode)
                    {
                        isTarget = true;
                    }
                    else if (request.ScopeType == 2 && targetTemplateIds.Count > 0)
                    {
                        isTarget = targetTemplateIds.Contains(s.ProductComponent.TemplateId);
                    }
                    else if (request.ScopeType == 3 && targetStepIds.Count > 0)
                    {
                        var compStepIds = await _db.TemplateSteps
                            .Where(ts => ts.TemplateId == s.ProductComponent.TemplateId)
                            .Select(ts => ts.TemplateStepId)
                            .ToListAsync();
                        isTarget = compStepIds.Any(id => targetStepIds.Contains(id));
                    }

                    if (isTarget)
                    {
                        s.Status = SessionStatus.InProgress;
                        s.FinalResult = SmartTorque.Core.Enums.SessionResult.Incomplete;
                        s.CurrentStepSequence = 1;
                        s.ReworkCount = unit.ReworkCount;
                        s.EndTime = null;
                        s.Remarks = $"Rework #{unit.ReworkCount}: {unit.LatestReworkReason}";
                    }
                }
            }
        }

        if (request.IsAllMode || request.ScopeType == 1)
        {
            job.ScopeType = AllocationScopeType.FullProduct;
            job.AllowedTemplateIds = null;
            job.AllowedStepIds = null;
            job.ScopeSummary = request.IsRework ? $"Rework #{unit?.ReworkCount ?? 1}: Full Product Rework" : "All Mode (Full Product Assembly)";
        }
        else
        {
            job.ScopeType = (AllocationScopeType)request.ScopeType;
            job.AllowedTemplateIds = request.AllowedTemplateIds;
            job.AllowedStepIds = request.AllowedStepIds;
            job.ScopeSummary = !string.IsNullOrWhiteSpace(request.ScopeSummary)
                ? request.ScopeSummary.Trim()
                : (request.IsRework ? $"Rework #{unit?.ReworkCount ?? 1}: Specific Operations" : (job.ScopeType == AllocationScopeType.FullProduct ? "All Mode (Full Product Assembly)" : "Custom Worker Scope"));
        }

        job.AssignedWorkerId = workerId;
        if (worker?.CurrentAssemblyLineId != null)
        {
            job.AssemblyLineId = worker.CurrentAssemblyLineId;
        }
        if (job.Status != JobStatus.Completed && job.Status != JobStatus.Rework)
        {
            job.Status = JobStatus.InProgress;
        }
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        // Ensure session is started/resumed with the equipped tools
        try
        {
            await _sessionService.StartOrResumeSessionAsync(
                job.JobId,
                workerId,
                torqueDeviceId: request.TorqueDeviceId,
                vernierDeviceId: request.VernierDeviceId,
                unitNo: request.UnitIndex);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"StartOrResumeSessionAsync error in ApplyCustomWorkload for Job {job.JobId}: {ex.Message}");
        }

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = job.JobId, unitNo = request.UnitIndex })
        });
    }

    [HttpPost]
    [Route("Worker/JobBoard/SwitchToAllMode/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SwitchToAllMode(int id, int? unitNo = null)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == id);
        if (job == null) return NotFound();

        var workerId = CurrentUserId();
        var worker = await _db.Users.FirstOrDefaultAsync(u => u.Id == workerId);

        job.ScopeType = AllocationScopeType.FullProduct;
        job.AllowedTemplateIds = null;
        job.AllowedStepIds = null;
        job.ScopeSummary = "All Mode (Full Product Assembly)";
        job.AssignedWorkerId = workerId;
        if (worker?.CurrentAssemblyLineId != null)
        {
            job.AssemblyLineId = worker.CurrentAssemblyLineId;
        }
        job.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Switched to All Mode. All product components and operations are now unlocked.";
        return RedirectToAction(nameof(Execute), new { id = job.JobId, unitNo = unitNo ?? 1 });
    }

    [HttpPost]
    [Route("Worker/JobBoard/CompleteUnitWork")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteUnitWork(int jobId, int unitNo)
    {
        var workerId = CurrentUserId();
        var job = await _db.Jobs
            .Include(j => j.Product)
            .FirstOrDefaultAsync(j => j.JobId == jobId);

        if (job == null) return NotFound();

        var jobUnit = await _db.JobUnits.FirstOrDefaultAsync(u => u.JobId == jobId && u.UnitIndex == unitNo);
        if (jobUnit != null)
        {
            jobUnit.Status = UnitAssemblyStatus.Completed;
            jobUnit.CompletedAt = DateTime.UtcNow;
            jobUnit.CompletedByWorkerId = workerId;

            var activeRework = await _db.JobUnitReworks
                .Where(r => r.JobUnitId == jobUnit.JobUnitId && r.Status == UnitAssemblyStatus.Rework)
                .OrderByDescending(r => r.ReworkIteration)
                .FirstOrDefaultAsync();

            if (activeRework != null)
            {
                activeRework.Status = UnitAssemblyStatus.Completed;
                activeRework.CompletedAt = DateTime.UtcNow;
                activeRework.CompletedByUserId = workerId;
                activeRework.CompletedByUserName = (await _db.Users.FindAsync(workerId))?.FullName ?? "Assembly Operator";
                activeRework.FinalResult = SmartTorque.Core.Enums.SessionResult.Pass;
                activeRework.ResolutionNotes = "Worker submitted completed workload.";
            }

            // Mark any active in-progress session for this unit as completed
            var activeSession = await _db.Sessions
                .Where(s => s.JobId == jobId && s.UnitNo == unitNo && s.Status == SessionStatus.InProgress)
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync();

            if (activeSession != null)
            {
                activeSession.Status = SessionStatus.Completed;
                activeSession.FinalResult = SmartTorque.Core.Enums.SessionResult.Pass;
                activeSession.EndTime = DateTime.UtcNow;
            }

            var completedUnits = await _db.JobUnits.CountAsync(u => u.JobId == jobId && u.Status == UnitAssemblyStatus.Completed);
            job.QuantityCompleted = completedUnits;
            if (job.QuantityCompleted >= job.Quantity)
            {
                job.Status = JobStatus.Completed;
            }
            job.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            TempData["Success"] = $"Unit #{unitNo} ({jobUnit.TokenNo}) work successfully submitted and quality report finalized.";
            if (activeSession != null)
            {
                return RedirectToAction(nameof(Summary), new { sessionId = activeSession.SessionId });
            }
        }

        return RedirectToAction(nameof(Execute), new { id = jobId, unitNo });
    }

    [HttpGet]
    public async Task<IActionResult> ResolveProductScope(int productId)
    {
        var workerId = CurrentUserId();
        var worker = await _db.Users
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .Include(u => u.Department)
            .FirstOrDefaultAsync(u => u.Id == workerId);

        var product = await _db.Products
            .Include(p => p.Components.OrderBy(c => c.AssemblyOrder))
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .FirstOrDefaultAsync(p => p.ProductId == productId);

        if (product == null)
            return Json(new { success = false, message = "Product not found." });

        var currentLineId = worker?.CurrentAssemblyLineId;
        var currentStationId = worker?.WorkStationId;
        var isDeptMode = !currentLineId.HasValue;

        // Check mapping
        ProductAssemblyMapping? mapping = null;
        if (currentLineId.HasValue)
        {
            if (currentStationId.HasValue)
            {
                mapping = await _db.ProductAssemblyMappings
                    .FirstOrDefaultAsync(m => m.ProductId == productId && m.AssemblyLineId == currentLineId.Value && m.WorkStationId == currentStationId.Value);
            }

            if (mapping == null)
            {
                mapping = await _db.ProductAssemblyMappings
                    .FirstOrDefaultAsync(m => m.ProductId == productId && m.AssemblyLineId == currentLineId.Value && m.WorkStationId == null);
            }
        }

        var allowedTemplateList = (mapping != null && !string.IsNullOrWhiteSpace(mapping.AllowedTemplateIds))
            ? mapping.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList()
            : new List<int>();

        var allowedStepList = (mapping != null && !string.IsNullOrWhiteSpace(mapping.AllowedStepIds))
            ? mapping.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToList()
            : new List<int>();

        var allProductSteps = product.Components
            .SelectMany(c => c.Template.Steps)
            .ToList();

        var components = product.Components.Select(c => {
            bool isCompInStationScope = isDeptMode || allowedTemplateList.Count == 0 || allowedTemplateList.Contains(c.TemplateId);
            return new
            {
                componentId = c.ProductComponentId,
                name = c.ComponentName,
                order = c.AssemblyOrder,
                templateId = c.TemplateId,
                templateCode = c.Template.TemplateCode,
                templateName = c.Template.Name,
                isStationScope = isCompInStationScope,
                steps = c.Template.Steps
                    .OrderBy(s => s.SequenceNo)
                    .Select(s => new
                    {
                        stepId = s.TemplateStepId,
                        sequenceNo = s.SequenceNo,
                        label = s.Label,
                        target = s.Target,
                        lowerLimit = s.Target - s.ToleranceMinus,
                        upperLimit = s.Target + s.TolerancePlus,
                        toolType = s.Instrument.ToString(),
                        isStationScope = isDeptMode || (isCompInStationScope && (allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)))
                    }).ToList()
            };
        }).ToList();

        int stationTotalCount = allProductSteps.Count(s => isDeptMode || (allowedStepList.Count == 0 || allowedStepList.Contains(s.TemplateStepId)));
        int totalCount = allProductSteps.Count;

        var fullStationTitle = worker?.WorkStation != null
            ? $"{worker.CurrentAssemblyLine?.AssemblyName} âž” Station: {worker.WorkStation.StationCode}"
            : (worker?.CurrentAssemblyLine?.AssemblyName ?? "Entire Department");

        var stationSummary = mapping?.ScopeSummary ?? (worker?.WorkStation != null ? $"Station {worker.WorkStation.StationCode} Scope" : (mapping != null ? "Line Workflow" : null));

        var lastSession = await _db.Sessions
            .Where(s => s.WorkerId == workerId && (s.TorqueDeviceId != null || s.VernierDeviceId != null))
            .OrderByDescending(s => s.SessionId)
            .FirstOrDefaultAsync();

        return Json(new
        {
            success = true,
            productId = product.ProductId,
            productCode = product.ProductCode,
            productName = product.ProductName,
            currentLineName = fullStationTitle,
            currentLineCode = worker?.CurrentAssemblyLine?.AssemblyCode ?? "",
            currentStationCode = worker?.WorkStation?.StationCode,
            currentStationName = worker?.WorkStation?.StationName,
            isStationMode = currentStationId.HasValue,
            isDeptMode = isDeptMode,
            isMapped = mapping != null,
            mappedScopeType = (int)(mapping?.ScopeType ?? AllocationScopeType.FullProduct),
            mappedScopeSummary = stationSummary,
            mappedTemplateIds = mapping?.AllowedTemplateIds,
            mappedStepIds = mapping?.AllowedStepIds,
            totalStepCount = totalCount,
            stationTotalStepCount = stationTotalCount,
            components = components,
            activeTorqueDeviceId = lastSession?.TorqueDeviceId,
            activeVernierDeviceId = lastSession?.VernierDeviceId
        });
    }

    [HttpGet]
    public async Task<IActionResult> CheckSerialDuplicate(string serial)
    {
        if (string.IsNullOrWhiteSpace(serial))
        {
            return Json(new { exists = false });
        }

        var cleanSerial = serial.Trim();

        // 1. Search in JobUnits (matches ProductSerial or TokenNo)
        var unit = await _db.JobUnits
            .Include(u => u.Job)
                .ThenInclude(j => j.Product)
            .Include(u => u.Job)
                .ThenInclude(j => j.AssignedWorker)
            .Include(u => u.CompletedByWorker)
            .Where(u => u.ProductSerial == cleanSerial || u.TokenNo == cleanSerial)
            .OrderByDescending(u => u.CreatedAt)
            .FirstOrDefaultAsync();

        Job? job = unit?.Job;

        // 2. If not found in JobUnits, check Jobs (matches Job.ProductSerial)
        if (job == null)
        {
            job = await _db.Jobs
                .Include(j => j.Product)
                .Include(j => j.AssignedWorker)
                .Include(j => j.Units)
                .Where(j => j.ProductSerial == cleanSerial)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefaultAsync();

            if (job != null)
            {
                unit = job.Units.FirstOrDefault(u => u.ProductSerial == cleanSerial)
                       ?? job.Units.OrderBy(u => u.UnitIndex).FirstOrDefault();
            }
        }

        if (job == null && unit == null)
        {
            return Json(new { exists = false });
        }

        var effectiveJob = job ?? unit!.Job;
        var unitIndex = unit?.UnitIndex ?? 1;
        var isCompleted = (unit?.Status == UnitAssemblyStatus.Completed) || (effectiveJob.Status == JobStatus.Completed);
        var isCancelled = effectiveJob.Status == JobStatus.Cancelled;
        var statusText = isCompleted ? "Completed" : (isCancelled ? "Cancelled" : (effectiveJob.Status == JobStatus.InProgress ? "In Progress" : "Pending"));
        var workerName = unit?.CompletedByWorker?.FullName ?? effectiveJob.AssignedWorker?.FullName ?? "Unassigned";
        var completedTime = unit?.CompletedAt ?? (isCompleted ? (DateTime?)effectiveJob.UpdatedAt : null);
        var completedDateStr = completedTime.HasValue ? completedTime.Value.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt") : "";

        return Json(new
        {
            exists = true,
            jobId = effectiveJob.JobId,
            jobNo = effectiveJob.JobNo,
            unitIndex = unitIndex,
            tokenNo = unit?.TokenNo ?? "",
            serial = cleanSerial,
            productId = effectiveJob.ProductId,
            productCode = effectiveJob.Product?.ProductCode ?? "",
            productName = effectiveJob.Product?.ProductName ?? "",
            salesOrder = effectiveJob.SalesOrder ?? "",
            poNo = effectiveJob.PONo ?? "",
            status = statusText,
            isCompleted = isCompleted,
            isCancelled = isCancelled,
            workerName = workerName,
            completedAt = completedDateStr,
            redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = effectiveJob.JobId, unitNo = unitIndex })
        });
    }

    public class ManualJobEntryRequest
    {
        public string? SalesOrder { get; set; }
        public string? PONo { get; set; }
        public int ProductId { get; set; }
        public string? SerialNo { get; set; }
        public int ScopeType { get; set; } // 1 = FullProduct, 2 = SpecificTemplates, 3 = SpecificSteps
        public string? AllowedTemplateIds { get; set; }
        public string? AllowedStepIds { get; set; }
        public string? ScopeSummary { get; set; }
        public bool IsAllMode { get; set; }
        public int? TorqueDeviceId { get; set; }
        public int? VernierDeviceId { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> StartManualEntryJob([FromBody] ManualJobEntryRequest request)
    {
        if (request.ProductId <= 0)
        {
            return Json(new { success = false, message = "Please select a valid Product / Item Code." });
        }

        var workerId = CurrentUserId();

        // Concurrency Check: Ensure tool is not currently locked by another operator
        if (request.TorqueDeviceId.HasValue || request.VernierDeviceId.HasValue)
        {
            var targetDevId = request.TorqueDeviceId ?? request.VernierDeviceId!.Value;
            var conflictingSession = await _db.Sessions
                .Include(s => s.Worker)
                .Include(s => s.Job)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId);

            if (conflictingSession != null)
            {
                var otherWorker = !string.IsNullOrWhiteSpace(conflictingSession.Worker?.FullName) 
                    ? conflictingSession.Worker.FullName 
                    : (!string.IsNullOrWhiteSpace(conflictingSession.Worker?.UserName) ? conflictingSession.Worker.UserName : "another operator");
                var dev = await _db.Devices.FindAsync(targetDevId);
                return Json(new { success = false, message = $"Tool '{(dev?.Name ?? "Selected Tool")}' is currently locked and in use by {otherWorker} on Job #{conflictingSession.Job?.JobNo ?? conflictingSession.JobId.ToString()}." });
            }
        }

        var worker = await _db.Users
            .Include(u => u.CurrentAssemblyLine)
            .Include(u => u.WorkStation)
            .FirstOrDefaultAsync(u => u.Id == workerId);

        var product = await _db.Products
            .Include(p => p.Components)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(p => p.ProductId == request.ProductId);

        if (product == null)
        {
            return Json(new { success = false, message = "Product not found." });
        }

        var cleanSo = string.IsNullOrWhiteSpace(request.SalesOrder) ? null : request.SalesOrder.Trim();
        var cleanPo = string.IsNullOrWhiteSpace(request.PONo) ? null : request.PONo.Trim();
        var cleanSerial = string.IsNullOrWhiteSpace(request.SerialNo) ? null : request.SerialNo.Trim();

        Job? job = null;
        JobUnit? targetUnit = null;

        // Strict Uniqueness Check: If serial number is provided, check if ANY unit/job exists with this serial
        if (!string.IsNullOrWhiteSpace(cleanSerial))
        {
            targetUnit = await _db.JobUnits
                .Include(u => u.Job)
                    .ThenInclude(j => j.Product)
                .Where(u => u.ProductSerial == cleanSerial || u.TokenNo == cleanSerial)
                .OrderByDescending(u => u.CreatedAt)
                .FirstOrDefaultAsync();

            if (targetUnit != null)
            {
                job = targetUnit.Job;
            }
            else
            {
                job = await _db.Jobs
                    .Include(j => j.Product)
                    .Include(j => j.Units)
                    .Where(j => j.ProductSerial == cleanSerial)
                    .OrderByDescending(j => j.CreatedAt)
                    .FirstOrDefaultAsync();

                if (job != null)
                {
                    targetUnit = job.Units.FirstOrDefault(u => u.ProductSerial == cleanSerial)
                                 ?? job.Units.OrderBy(u => u.UnitIndex).FirstOrDefault();
                }
            }

            if (job != null)
            {
                // DUPLICATE DETECTED: Serial No is globally unique!
                bool isCompleted = (targetUnit?.Status == UnitAssemblyStatus.Completed) || (job.Status == JobStatus.Completed);
                if (isCompleted)
                {
                    var compTime = targetUnit?.CompletedAt ?? (DateTime?)job.UpdatedAt;
                    var compDate = compTime.HasValue ? compTime.Value.ToLocalTime().ToString("dd MMM yyyy, hh:mm tt") : "earlier";
                    return Json(new
                    {
                        success = false,
                        isDuplicate = true,
                        isCompleted = true,
                        message = $"Serial No '{cleanSerial}' is already completed on Job #{job.JobNo} (Unit #{targetUnit?.UnitIndex ?? 1}) on {compDate}. Duplicate orders cannot be created.",
                        jobId = job.JobId,
                        unitNo = targetUnit?.UnitIndex ?? 1,
                        redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = job.JobId, unitNo = targetUnit?.UnitIndex ?? 1 })
                    });
                }

                // If not completed, route the operator directly to the existing active/pending order unit!
                job.AssignedWorkerId = workerId;
                if (worker?.CurrentAssemblyLineId != null && job.AssemblyLineId == null)
                {
                    job.AssemblyLineId = worker.CurrentAssemblyLineId;
                }
                if (cleanSo != null && string.IsNullOrWhiteSpace(job.SalesOrder)) job.SalesOrder = cleanSo;
                if (cleanPo != null && string.IsNullOrWhiteSpace(job.PONo)) job.PONo = cleanPo;

                if (targetUnit == null)
                {
                    targetUnit = job.Units.FirstOrDefault(u => u.Status != UnitAssemblyStatus.Completed);
                }

                if (targetUnit != null)
                {
                    targetUnit.ProductSerial = cleanSerial;
                    if (targetUnit.Status == UnitAssemblyStatus.Pending)
                    {
                        targetUnit.Status = UnitAssemblyStatus.InProgress;
                        targetUnit.StartedAt = DateTime.UtcNow;
                    }
                }

                job.Status = JobStatus.InProgress;
                job.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                int existingUnitNo = targetUnit?.UnitIndex ?? 1;

                try
                {
                    await _sessionService.StartOrResumeSessionAsync(
                        job.JobId,
                        workerId,
                        torqueDeviceId: request.TorqueDeviceId,
                        vernierDeviceId: request.VernierDeviceId,
                        unitNo: existingUnitNo);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error starting session for existing unit {job.JobId}: {ex.Message}");
                }

                return Json(new
                {
                    success = true,
                    isExisting = true,
                    message = $"Serial No '{cleanSerial}' already exists in Job #{job.JobNo} (Unit #{existingUnitNo}). Directing to existing unit.",
                    redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = job.JobId, unitNo = existingUnitNo })
                });
            }
        }

        // If not found by serial, check if an open job exists matching SO, PO, and Product
        if (job == null)
        {
            var jobQuery = _db.Jobs
                .Include(j => j.Units)
                .Where(j => j.ProductId == product.ProductId &&
                            j.Status != JobStatus.Completed &&
                            j.Status != JobStatus.Cancelled);

            if (!string.IsNullOrWhiteSpace(cleanSo))
                jobQuery = jobQuery.Where(j => j.SalesOrder == cleanSo);
            if (!string.IsNullOrWhiteSpace(cleanPo))
                jobQuery = jobQuery.Where(j => j.PONo == cleanPo);
            if (worker?.CurrentAssemblyLineId != null)
                jobQuery = jobQuery.Where(j => j.AssemblyLineId == worker.CurrentAssemblyLineId || j.AssemblyLineId == null);

            job = await jobQuery.OrderByDescending(j => j.CreatedAt).FirstOrDefaultAsync();
        }

        if (job != null)
        {
            job.AssignedWorkerId = workerId;
            if (worker?.CurrentAssemblyLineId != null && job.AssemblyLineId == null)
            {
                job.AssemblyLineId = worker.CurrentAssemblyLineId;
            }
            if (cleanSo != null && string.IsNullOrWhiteSpace(job.SalesOrder)) job.SalesOrder = cleanSo;
            if (cleanPo != null && string.IsNullOrWhiteSpace(job.PONo)) job.PONo = cleanPo;
            if (cleanSerial != null && string.IsNullOrWhiteSpace(job.ProductSerial)) job.ProductSerial = cleanSerial;

            if (targetUnit == null)
            {
                targetUnit = job.Units.FirstOrDefault(u => u.Status != UnitAssemblyStatus.Completed && (u.ProductSerial == null || u.ProductSerial == cleanSerial));
                if (targetUnit == null)
                {
                    job.Quantity++;
                    targetUnit = new JobUnit
                    {
                        JobId = job.JobId,
                        UnitIndex = job.Quantity,
                        TokenNo = TokenGenerator.GenerateProductUnitToken(),
                        ProductSerial = cleanSerial,
                        Status = UnitAssemblyStatus.InProgress,
                        StartedAt = DateTime.UtcNow,
                        CreatedAt = DateTime.UtcNow
                    };
                    _db.JobUnits.Add(targetUnit);
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(cleanSerial))
                    {
                        targetUnit.ProductSerial = cleanSerial;
                    }
                    if (targetUnit.Status == UnitAssemblyStatus.Pending)
                    {
                        targetUnit.Status = UnitAssemblyStatus.InProgress;
                        targetUnit.StartedAt = DateTime.UtcNow;
                    }
                }
            }
            else
            {
                if (targetUnit.Status == UnitAssemblyStatus.Pending)
                {
                    targetUnit.Status = UnitAssemblyStatus.InProgress;
                    targetUnit.StartedAt = DateTime.UtcNow;
                }
            }
        }
        else
        {
            var lineCode = worker?.CurrentAssemblyLine?.AssemblyCode ?? "MAN";
            var todayStr = DateTime.UtcNow.ToString("yyyyMMdd");
            var countToday = await _db.Jobs.CountAsync(j => j.CreatedAt >= DateTime.UtcNow.Date);
            var baseJobNo = $"JOB-{todayStr}-{(countToday + 1):D4}-{lineCode}";
            var jobNo = baseJobNo;
            var attempt = 1;
            while (await _db.Jobs.AnyAsync(j => j.JobNo == jobNo))
            {
                jobNo = $"{baseJobNo}-P{attempt++}";
            }

            job = new Job
            {
                JobNo = jobNo,
                ProductId = product.ProductId,
                AssemblyLineId = worker?.CurrentAssemblyLineId,
                AssignedWorkerId = workerId,
                SalesOrder = cleanSo,
                PONo = cleanPo,
                ProductSerial = cleanSerial,
                Quantity = 1,
                QuantityCompleted = 0,
                EstimatedWorkload = 1.0m,
                Priority = PriorityLevel.White,
                Status = JobStatus.InProgress,
                AssignmentMode = AssignmentMode.Manual,
                ExecutionMode = ExecutionMode.Manual,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            targetUnit = new JobUnit
            {
                UnitIndex = 1,
                TokenNo = TokenGenerator.GenerateProductUnitToken(),
                ProductSerial = cleanSerial,
                Status = UnitAssemblyStatus.InProgress,
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };
            job.Units.Add(targetUnit);
            _db.Jobs.Add(job);
        }

        // Apply Scope
        if (request.IsAllMode || request.ScopeType == 1)
        {
            job.ScopeType = AllocationScopeType.FullProduct;
            job.AllowedTemplateIds = null;
            job.AllowedStepIds = null;
            job.ScopeSummary = "All Mode (Full Product Assembly)";
        }
        else
        {
            job.ScopeType = (AllocationScopeType)request.ScopeType;
            job.AllowedTemplateIds = request.AllowedTemplateIds;
            job.AllowedStepIds = request.AllowedStepIds;
            job.ScopeSummary = !string.IsNullOrWhiteSpace(request.ScopeSummary)
                ? request.ScopeSummary.Trim()
                : (job.ScopeType == AllocationScopeType.FullProduct ? "All Mode (Full Product Assembly)" : "Station Mapped Scope");
        }

        job.Status = JobStatus.InProgress;
        job.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        int unitNo = targetUnit?.UnitIndex ?? 1;

        try
        {
            await _sessionService.StartOrResumeSessionAsync(
                job.JobId,
                workerId,
                torqueDeviceId: request.TorqueDeviceId,
                vernierDeviceId: request.VernierDeviceId,
                unitNo: unitNo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error starting session for manual job {job.JobId}: {ex.Message}");
        }

        return Json(new
        {
            success = true,
            redirectUrl = Url.Action("Execute", "JobBoard", new { area = "Worker", id = job.JobId, unitNo = unitNo })
        });
    }

    public class SetStationToolRequest
    {
        public int? TorqueDeviceId { get; set; }
        public int? VernierDeviceId { get; set; }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Worker/JobBoard/SetStationTool")]
    public async Task<IActionResult> SetStationTool([FromBody] SetStationToolRequest request)
    {
        var workerId = CurrentUserId();
        if (request.TorqueDeviceId.HasValue || request.VernierDeviceId.HasValue)
        {
            var targetDevId = request.TorqueDeviceId ?? request.VernierDeviceId!.Value;

            // Auto-expire stale conflicting sessions (>30 min)
            var staleConflicting = await _db.Sessions
                .Where(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId
                    && s.StartTime < DateTime.UtcNow.AddMinutes(-30))
                .ToListAsync();
            foreach (var stale in staleConflicting)
            {
                stale.TorqueDeviceId = null;
                stale.VernierDeviceId = null;
                stale.Status = SessionStatus.OnHold;
            }
            if (staleConflicting.Count > 0) await _db.SaveChangesAsync();

            var conflictingSession = await _db.Sessions
                .Include(s => s.Worker)
                .Include(s => s.Job)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress && s.EndTime == null 
                    && (s.TorqueDeviceId == targetDevId || s.VernierDeviceId == targetDevId)
                    && s.WorkerId != workerId);

            if (conflictingSession != null)
            {
                var otherWorker = !string.IsNullOrWhiteSpace(conflictingSession.Worker?.FullName) 
                    ? conflictingSession.Worker.FullName 
                    : (!string.IsNullOrWhiteSpace(conflictingSession.Worker?.UserName) ? conflictingSession.Worker.UserName : "another operator");
                var dev = await _db.Devices.FindAsync(targetDevId);
                return Json(new { success = false, message = $"Tool '{(dev?.Name ?? "Selected Tool")}' is currently locked and in use by {otherWorker} on Job #{conflictingSession.Job?.JobNo ?? conflictingSession.JobId.ToString()}." });
            }

            var selectedDev = await _db.Devices.FindAsync(targetDevId);

            // Update in-memory preference immediately
            _workerStationTools[workerId] = (request.TorqueDeviceId, request.VernierDeviceId);

            // Persist tool selection immediately across all active sessions for this worker
            var activeWorkerSessions = await _db.Sessions
                .Where(s => s.WorkerId == workerId && s.Status == SessionStatus.InProgress && s.EndTime == null)
                .ToListAsync();

            if (activeWorkerSessions.Count > 0)
            {
                foreach (var s in activeWorkerSessions)
                {
                    if (request.TorqueDeviceId.HasValue) s.TorqueDeviceId = request.TorqueDeviceId.Value;
                    if (request.VernierDeviceId.HasValue) s.VernierDeviceId = request.VernierDeviceId.Value;
                }
            }

            // Also update worker's most recent session so server-side tool selection persists across browser refreshes & tablet logins
            var recentSession = await _db.Sessions
                .Where(s => s.WorkerId == workerId)
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync();
            if (recentSession != null)
            {
                if (request.TorqueDeviceId.HasValue) recentSession.TorqueDeviceId = request.TorqueDeviceId.Value;
                if (request.VernierDeviceId.HasValue) recentSession.VernierDeviceId = request.VernierDeviceId.Value;
            }

            var openWindows = await _db.SessionActivityWindows
                .Where(w => w.WorkerId == workerId && w.WindowEnd == null)
                .ToListAsync();
            foreach (var w in openWindows)
            {
                w.DeviceId = targetDevId;
            }

            await _db.SaveChangesAsync();

            return Json(new
            {
                success = true,
                deviceId = targetDevId,
                deviceName = selectedDev?.Name ?? "Station Tool",
                transmitterId = selectedDev?.TransmitterId ?? "",
                instrumentType = (int)(selectedDev?.InstrumentType ?? InstrumentType.Torque)
            });
        }

        return Json(new { success = false, message = "No tool specified." });
    }

    public class SyncSessionToolRequest
    {
        public long? SessionId { get; set; }
        public int? JobId { get; set; }
        public int? TorqueDeviceId { get; set; }
        public int? VernierDeviceId { get; set; }
    }

    [HttpPost]
    [Route("Worker/JobBoard/SyncSessionTool")]
    public async Task<IActionResult> SyncSessionTool([FromBody] SyncSessionToolRequest request)
    {
        var workerId = CurrentUserId();
        if (request.TorqueDeviceId.HasValue || request.VernierDeviceId.HasValue)
        {
            var targetDevId = request.TorqueDeviceId ?? request.VernierDeviceId!.Value;
            _workerStationTools[workerId] = (request.TorqueDeviceId, request.VernierDeviceId);

            var sessionsToUpdate = await _db.Sessions
                .Where(s => s.WorkerId == workerId && s.Status != SessionStatus.Completed && (request.SessionId == null || s.SessionId == request.SessionId.Value) && (request.JobId == null || s.JobId == request.JobId.Value))
                .ToListAsync();

            if (sessionsToUpdate.Count == 0 && request.SessionId.HasValue)
            {
                var single = await _db.Sessions.FirstOrDefaultAsync(s => s.SessionId == request.SessionId.Value && s.WorkerId == workerId);
                if (single != null) sessionsToUpdate.Add(single);
            }

            foreach (var s in sessionsToUpdate)
            {
                if (request.TorqueDeviceId.HasValue) s.TorqueDeviceId = request.TorqueDeviceId.Value;
                if (request.VernierDeviceId.HasValue) s.VernierDeviceId = request.VernierDeviceId.Value;
            }

            var openWindows = await _db.SessionActivityWindows
                .Where(w => w.WorkerId == workerId && w.WindowEnd == null && (request.SessionId == null || w.SessionId == request.SessionId.Value))
                .ToListAsync();
            foreach (var w in openWindows)
            {
                w.DeviceId = targetDevId;
            }

            await _db.SaveChangesAsync();

            var dev = await _db.Devices.FindAsync(targetDevId);
            return Json(new
            {
                success = true,
                deviceId = targetDevId,
                deviceName = dev?.Name ?? "Station Tool",
                transmitterId = dev?.TransmitterId ?? "",
                instrumentType = (int)(dev?.InstrumentType ?? InstrumentType.Torque)
            });
        }
        return Json(new { success = false, message = "No tool specified." });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("Worker/JobBoard/ReleaseStationTool")]
    public async Task<IActionResult> ReleaseStationTool()
    {
        var workerId = CurrentUserId();
        try
        {
            var activeSessions = await _db.Sessions
                .Where(s => s.WorkerId == workerId && s.Status == SessionStatus.InProgress && s.EndTime == null)
                .ToListAsync();

            foreach (var s in activeSessions)
            {
                s.TorqueDeviceId = null;
                s.VernierDeviceId = null;
            }

            var openWindows = await _db.SessionActivityWindows
                .Where(w => w.WorkerId == workerId && w.WindowEnd == null)
                .ToListAsync();

            foreach (var w in openWindows)
            {
                w.WindowEnd = DateTime.UtcNow;
            }

            _workerStationTools.TryRemove(workerId, out _);

            await _db.SaveChangesAsync();
            return Json(new { success = true, message = "Station tool released successfully." });
        }
        catch (Exception ex)
        {
            return Json(new { success = false, message = ex.Message });
        }
    }
}
