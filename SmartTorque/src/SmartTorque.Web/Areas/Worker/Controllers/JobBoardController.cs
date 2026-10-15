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
    private readonly IHubContext<LiveReadingHub> _hubContext;

    public JobBoardController(
        AppDbContext db,
        UserManager<ApplicationUser> userManager,
        ISessionService sessionService,
        IHubContext<LiveReadingHub> hubContext)
    {
        _db = db;
        _userManager = userManager;
        _sessionService = sessionService;
        _hubContext = hubContext;
    }

    private string CurrentUserId() => _userManager.GetUserId(User) ?? string.Empty;

    [HttpGet]
    public async Task<IActionResult> Index(string? filter = "all")
    {
        var workerId = CurrentUserId();
        var worker = await _userManager.GetUserAsync(User);
        var activeFilter = (filter ?? "all").ToLowerInvariant();

        // Query accessible open or assigned jobs
        var baseQuery = _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Where(j => j.Status != JobStatus.Completed && j.Status != JobStatus.Cancelled)
            .Where(j => j.AssignedWorkerId == null || j.AssignedWorkerId == workerId);

        var allJobs = await baseQuery.ToListAsync();

        var cards = allJobs.Select(j => new WorkerJobCardViewModel
        {
            JobId = j.JobId,
            JobNo = j.JobNo,
            ProductCode = j.Product.ProductCode,
            ProductName = j.Product.ProductName,
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

        var totalCount = cards.Count;
        var myJobsCount = cards.Count(c => c.IsAssignedToCurrentWorker);
        var blackCount = cards.Count(c => c.Priority == PriorityLevel.Black);
        var openCount = cards.Count(c => c.IsOpen);

        var filtered = activeFilter switch
        {
            "my-jobs" => cards.Where(c => c.IsAssignedToCurrentWorker).ToList(),
            "black" => cards.Where(c => c.Priority == PriorityLevel.Black).ToList(),
            "open" => cards.Where(c => c.IsOpen).ToList(),
            _ => cards
        };

        // Pin Black priority jobs to top, then sort by Priority, DueDate, JobNo
        var ordered = filtered
            .OrderBy(c => c.Priority == PriorityLevel.Black ? 0 : 1)
            .ThenBy(c => c.Priority)
            .ThenBy(c => c.DueDate ?? DateOnly.MaxValue)
            .ThenBy(c => c.JobNo)
            .ToList();

        var vm = new WorkerJobBoardViewModel
        {
            WorkerName = worker?.FullName ?? worker?.UserName ?? "Worker",
            WorkerId = workerId,
            ActiveFilter = activeFilter,
            TotalCount = totalCount,
            MyJobsCount = myJobsCount,
            BlackCount = blackCount,
            OpenCount = openCount,
            Jobs = ordered
        };

        return View(vm);
    }

    [HttpPost]
    [Route("Worker/JobBoard/Start/{id}")]
    [Route("Worker/Start/{id}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(int id)
    {
        var workerId = CurrentUserId();
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == id);
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

        try
        {
            await _sessionService.StartOrResumeSessionAsync(id, workerId);
            TempData["Success"] = $"Started job {job.JobNo}.";
            return RedirectToAction(nameof(Execute), new { id });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = $"Job {job.JobNo} was concurrently updated by another user. Please refresh.";
            return RedirectToAction(nameof(Index));
        }
    }

    [HttpGet]
    public async Task<IActionResult> Execute(int id)
    {
        var workerId = CurrentUserId();
        try
        {
            var state = await _sessionService.GetExecutionStateAsync(id, workerId);
            return View(state);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
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