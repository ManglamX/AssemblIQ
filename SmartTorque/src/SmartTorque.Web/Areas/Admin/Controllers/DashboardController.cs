using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Entities;
using SmartTorque.Services;

namespace SmartTorque.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = "Admin")]
public class DashboardController : Controller
{
    private readonly IReportService _reportService;
    private readonly UserManager<ApplicationUser>? _userManager;

    public DashboardController(IReportService reportService, UserManager<ApplicationUser>? userManager = null)
    {
        _reportService = reportService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var user = _userManager != null ? await _userManager.GetUserAsync(User) : null;
        var isPlantHead = user != null && (user.IsMainAdmin || user.IsPlantHead);
        int? effectiveDeptId = isPlantHead ? null : user?.DepartmentId;

        var stats = await _reportService.GetAdminDashboardStatsAsync(effectiveDeptId, ct);
        return View(stats);
    }
}
