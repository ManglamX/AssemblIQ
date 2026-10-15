using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Hubs;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin,Worker")]
[Route("dev/simulator")]
public class DevSimulatorController : Controller
{
    private readonly AppDbContext _db;
    private readonly IDeviceService _deviceService;

    public DevSimulatorController(AppDbContext db, IDeviceService deviceService)
    {
        _db = db;
        _deviceService = deviceService;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var devices = await _deviceService.ListAsync();

        // Get any currently active in-progress session
        var activeSession = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Include(s => s.Worker)
            .OrderByDescending(s => s.StartTime)
            .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress);

        ViewBag.ActiveSession = activeSession;
        ViewData["Title"] = "Hardware Simulator (Dev)";
        return View(devices);
    }

    [HttpPost("trigger-alert")]
    public async Task<IActionResult> TriggerAlert([FromServices] IHubContext<LiveReadingHub> hubContext, [FromForm] string? message)
    {
        var msg = string.IsNullOrWhiteSpace(message)
            ? "CRITICAL ALERT: Urgent Black-priority assembly job dispatched to production floor!"
            : message;
        await hubContext.Clients.Group("workers").SendAsync("BlackJobAlert", new { message = msg, timestamp = DateTime.UtcNow });
        return Json(new { success = true, message = msg });
    }
}