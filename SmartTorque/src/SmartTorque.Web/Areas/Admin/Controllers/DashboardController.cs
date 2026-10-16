using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Services;

namespace SmartTorque.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly IReportService _reportService;

    public DashboardController(IReportService reportService)
    {
        _reportService = reportService;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var stats = await _reportService.GetAdminDashboardStatsAsync(ct);
        return View(stats);
    }
}