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
    private readonly IUserContextService? _userContext;
    private readonly UserManager<ApplicationUser>? _userManager;

    public DashboardController(
        IReportService reportService,
        IUserContextService? userContext = null,
        UserManager<ApplicationUser>? userManager = null)
    {
        _reportService = reportService;
        _userContext = userContext;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var ctx = _userContext != null ? await _userContext.GetCurrentUserContextAsync(User, ct) : null;
        var user = _userManager != null ? await _userManager.GetUserAsync(User) : null;

        var isPlantHead = (ctx?.HasFullPlantAccess ?? false) || (user != null && (user.IsMainAdmin || user.IsPlantHead));
        int? effectiveDeptId = isPlantHead ? null : (ctx?.DepartmentId ?? user?.DepartmentId);

        var stats = await _reportService.GetAdminDashboardStatsAsync(effectiveDeptId, ct);
        return View(stats);
    }
}
