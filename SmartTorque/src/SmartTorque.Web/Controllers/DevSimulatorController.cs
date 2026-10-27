using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

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
}
