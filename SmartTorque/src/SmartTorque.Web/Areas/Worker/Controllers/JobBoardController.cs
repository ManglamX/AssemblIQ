using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Identity;

using Microsoft.AspNetCore.Mvc;

using Microsoft.AspNetCore.SignalR;

using Microsoft.EntityFrameworkCore;



using SmartTorque.Core.Entities;

using SmartTorque.Core.Enums;

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



    public JobBoardController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ISessionService sessionService,
        IHierarchyService hierarchy,
        IHubContext<LiveReadingHub> hubContext)
    {
        _db = db;
        _userManager = userManager;
        _sessionService = sessionService;
        _hierarchy = hierarchy;
        _hubContext = hubContext;
    }



    private string CurrentUserId() => _userManager.GetUserId(User) ?? string.Empty;



    [HttpGet]

    public async Task<IActionResult> Index(string? filter = "my-jobs")

    {

        var workerId = CurrentUserId();

        var worker = await _db.Users

            .Include(u => u.CurrentAssemblyLine)

            .FirstOrDefaultAsync(u => u.Id == workerId);



        var activeFilter = (filter ?? "my-jobs").ToLowerInvariant();

        if (activeFilter != "open" && activeFilter != "my-jobs")

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



        // Fetch all active assembly lines for the station switcher

        var linesQuery = _db.AssemblyLines
            .AsNoTracking()
            .Where(a => a.IsActive);

        if (worker?.DepartmentId.HasValue == true)
        {
            linesQuery = linesQuery.Where(a => a.DepartmentId == worker.DepartmentId.Value);
        }

        var activeLines = await linesQuery
            .OrderBy(a => a.AssemblyCode)
            .ToListAsync();



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

            baseQuery = baseQuery.Where(j =>

                j.AssignedWorkerId == workerId ||

                (j.AssignedWorkerId == null && j.AssemblyLineId == null));

        }



        var allJobs = await baseQuery.ToListAsync();



        var cards = allJobs.Select(j => new WorkerJobCardViewModel

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

            QuantityCompleted = j.QuantityCompleted,

            DueDate = j.DueDate,

            AssignedWorkerId = j.AssignedWorkerId,

            AssignedWorkerName = j.AssignedWorker?.FullName ?? j.AssignedWorker?.UserName,

            IsAssignedToCurrentWorker = j.AssignedWorkerId == workerId

        }).ToList();



        var myJobsCount = cards.Count(c => c.IsAssignedToCurrentWorker);

        var openCount = cards.Count(c => c.IsOpen);



        var filtered = activeFilter switch

        {

            "open" => cards.Where(c => c.IsOpen).ToList(),

            _ => cards.Where(c => c.IsAssignedToCurrentWorker).ToList()

        };



        var ordered = filtered

            .OrderBy(c => c.Priority == PriorityLevel.Black ? 0 : 1)

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



        var vm = new WorkerJobBoardViewModel

        {

            WorkerName = worker?.FullName ?? worker?.UserName ?? "Worker",

            WorkerId = workerId,

            CurrentAssemblyLineId = worker?.CurrentAssemblyLineId,

            CurrentAssemblyCode = worker?.CurrentAssemblyLine?.AssemblyCode,

            CurrentAssemblyName = worker?.CurrentAssemblyLine?.AssemblyName,

            WorkerActiveWorkload = activeWorkload,

            AvailableAssemblyLines = activeLines,

            ActiveFilter = activeFilter,

            TotalCount = cards.Count,

            MyJobsCount = myJobsCount,

            BlackCount = 0,

            OpenCount = openCount,

            IncompleteAssignedJobsCount = incompleteAssignedCount,

            Jobs = ordered

        };


        return View(vm);

    }



    [HttpPost]

    [Route("Worker/JobBoard/SetAssemblyLine")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SetAssemblyLine(int? assemblyLineId, string? returnUrl = null)

    {

        var workerId = CurrentUserId();

        var worker = await _db.Users

            .Include(u => u.CurrentAssemblyLine)

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

            TempData["Success"] = $"Station switched to Assembly Line: {line.AssemblyCode} — {line.AssemblyName}.";

        }

        else

        {

            worker.CurrentAssemblyLineId = null;

            TempData["Success"] = "Assembly line unassigned.";

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

    public async Task<IActionResult> Start(int id, int? torqueDeviceId = null, int? vernierDeviceId = null, int? unitNo = null, string? returnUrl = null)

    {

        var workerId = CurrentUserId();

        var job = await _db.Jobs

            .Include(j => j.AssemblyLine)

            .FirstOrDefaultAsync(j => j.JobId == id);

        if (job == null) return NotFound();



        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)

        {

            TempData["Error"] = $"Job {job.JobNo} is already {job.Status.ToString().ToLowerInvariant()}.";

            return RedirectToAction(nameof(Index));

        }



        if (job.Status == JobStatus.InProgress && job.AssignedWorkerId != null && job.AssignedWorkerId != workerId)

        {

            TempData["Error"] = $"Job {job.JobNo} is currently in progress by another worker.";

            return RedirectToAction(nameof(Index));

        }



        // If job is unassigned, worker is claiming it: verify current assigned workload is completed

        if (string.IsNullOrWhiteSpace(job.AssignedWorkerId))

        {

            var hasIncompleteWorkload = await _db.Jobs

                .AnyAsync(j => j.AssignedWorkerId == workerId &&

                               j.JobId != id &&

                               j.Status != JobStatus.Completed &&

                               j.Status != JobStatus.Cancelled);



            if (hasIncompleteWorkload)

            {

                TempData["Error"] = "Cannot claim job: You must complete your currently assigned workload before claiming new jobs.";

                return RedirectToAction(nameof(Index), new { filter = "my-jobs" });

            }

        }



        // Strict assembly line validation:

        if (job.AssemblyLineId.HasValue)

        {

            var workerUser = await _db.Users.Include(u => u.CurrentAssemblyLine).FirstOrDefaultAsync(u => u.Id == workerId);

            if (workerUser?.CurrentAssemblyLineId != job.AssemblyLineId.Value)

            {

                TempData["Error"] = $"Cannot start job {job.JobNo}: Designated for assembly line '{job.AssemblyLine?.AssemblyCode ?? "Other"}', but you are currently stationed at '{workerUser?.CurrentAssemblyLine?.AssemblyCode ?? "Unassigned"}'. Switch stations first.";

                return RedirectToAction(nameof(Index));

            }

        }



        try

        {

            await _sessionService.StartOrResumeSessionAsync(id, workerId, torqueDeviceId: torqueDeviceId, vernierDeviceId: vernierDeviceId, unitNo: unitNo);

            TempData["Success"] = $"Started job {job.JobNo}.";

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



    [HttpPost]

    [Route("Worker/JobBoard/PairTool")]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> PairTool(int jobId, long sessionId, int? torqueDeviceId = null, int? vernierDeviceId = null)

    {

        await _sessionService.PairDevicesAsync(sessionId, torqueDeviceId, vernierDeviceId);

        TempData["Success"] = "Hardware tool paired successfully.";

        return RedirectToAction(nameof(Execute), new { id = jobId });

    }



    [HttpGet]

    public async Task<IActionResult> Execute(int id)

    {

        var workerId = CurrentUserId();

        try

        {

            var state = await _sessionService.GetExecutionStateAsync(id, workerId);

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

        var history = await _sessionService.GetWorkerHistoryAsync(workerId, filter);

        ViewData["ActiveFilter"] = filter ?? "all";

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
    [Route("Worker/Profile/GetStationsForLine")]
    public async Task<IActionResult> GetStationsForLine(int lineId)
    {
        var stations = await _hierarchy.GetWorkStationsAsync(lineId);
        return Json(stations.Select(s => new {
            id = s.WorkStationId,
            code = s.StationCode,
            name = $"{s.StationName} ({(s.StationType == StationType.Subcell ? "Subcell" : "Station")})"
        }));
    }



    [HttpGet]

    [Route("Worker/JobBoard/Units/{id}")]

    public async Task<IActionResult> GetUnits(int id)

    {

        var units = await _sessionService.GetJobUnitsAsync(id);

        return Json(units.Select(u => new

        {

            u.JobUnitId,

            u.UnitIndex,

            u.TokenNo,

            Status = u.Status.ToString()

        }));

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SwitchComponent(int id, int productComponentId)

    {

        var workerId = CurrentUserId();

        await _sessionService.SwitchComponentAsync(id, productComponentId, workerId);

        return RedirectToAction(nameof(Execute), new { id });

    }



    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> SwitchUnit(int id, int unitNo)

    {

        var workerId = CurrentUserId();

        await _sessionService.SwitchUnitAsync(id, unitNo, workerId);

        return RedirectToAction(nameof(Execute), new { id });

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

    [Route("Worker/JobBoard/SubmitReading")]

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

}