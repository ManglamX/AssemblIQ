using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Web.ViewModels.User;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class UserController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly AppDbContext _db;
    private readonly ILogger<UserController> _logger;

    public UserController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        AppDbContext db,
        ILogger<UserController> logger)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
    }

    [HttpGet]
    [Route("User")]
    [Route("Admin/Users")]
    public async Task<IActionResult> Index(string? filter = null)
    {
        var users = await _userManager.Users.OrderByDescending(u => u.CreatedAt).ToListAsync();

        var cards = new List<UserCardViewModel>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            var currentRole = roles.FirstOrDefault() ?? u.RequestedRole ?? "Worker";

            cards.Add(new UserCardViewModel
            {
                Id = u.Id,
                FullName = u.FullName,
                Email = u.Email ?? string.Empty,
                EmployeeNo = u.EmployeeNo,
                RegistrationStatus = u.RegistrationStatus,
                RequestedRole = u.RequestedRole ?? currentRole,
                CurrentRole = currentRole,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                ReviewedAt = u.ReviewedAt,
                ReviewedBy = u.ReviewedBy,
                RejectionReason = u.RejectionReason
            });
        }

        var pendingCount = cards.Count(c => c.RegistrationStatus == RegistrationStatus.Pending);
        var workersCount = cards.Count(c => c.CurrentRole == "Worker" && c.RegistrationStatus == RegistrationStatus.Approved);
        var adminsCount = cards.Count(c => c.CurrentRole == "Admin" && c.RegistrationStatus == RegistrationStatus.Approved);
        var rejectedCount = cards.Count(c => c.RegistrationStatus == RegistrationStatus.Rejected);

        // Default to pending if any pending exist, otherwise all
        var activeFilter = filter?.ToLowerInvariant() ?? (pendingCount > 0 ? "pending" : "all");

        var filtered = activeFilter switch
        {
            "pending" => cards.Where(c => c.RegistrationStatus == RegistrationStatus.Pending).ToList(),
            "workers" => cards.Where(c => c.CurrentRole == "Worker" && c.RegistrationStatus == RegistrationStatus.Approved).ToList(),
            "admins" => cards.Where(c => c.CurrentRole == "Admin" && c.RegistrationStatus == RegistrationStatus.Approved).ToList(),
            "rejected" => cards.Where(c => c.RegistrationStatus == RegistrationStatus.Rejected).ToList(),
            _ => cards
        };

        var vm = new UserListViewModel
        {
            ActiveFilter = activeFilter,
            PendingCount = pendingCount,
            WorkersCount = workersCount,
            AdminsCount = adminsCount,
            RejectedCount = rejectedCount,
            TotalCount = cards.Count,
            Users = filtered
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/Approve")]
    public async Task<IActionResult> Approve(string userId, string? assignedRole)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        var role = !string.IsNullOrWhiteSpace(assignedRole) ? assignedRole.Trim() : (user.RequestedRole ?? "Worker");

        user.RegistrationStatus = RegistrationStatus.Approved;
        user.IsActive = true;
        user.RejectionReason = null;
        user.ReviewedAt = DateTime.UtcNow;
        user.ReviewedBy = User?.Identity?.Name ?? "Admin";
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        // Update role
        var currentRoles = await _userManager.GetRolesAsync(user);
        if (!currentRoles.Contains(role))
        {
            if (currentRoles.Any())
            {
                await _userManager.RemoveFromRolesAsync(user, currentRoles);
            }
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new IdentityRole(role));
            }
            await _userManager.AddToRoleAsync(user, role);
        }

        _logger.LogInformation("User {Email} approved by {Admin} as {Role}", user.Email, User?.Identity?.Name, role);
        if (TempData != null)
        {
            TempData["Success"] = $"User {user.FullName} ({user.Email}) has been approved as {role}.";
        }

        return RedirectToAction(nameof(Index), new { filter = "pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/Reject")]
    public async Task<IActionResult> Reject(string userId, string? reason)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        user.RegistrationStatus = RegistrationStatus.Rejected;
        user.IsActive = false;
        user.RejectionReason = !string.IsNullOrWhiteSpace(reason) ? reason.Trim() : "Access request was rejected by an administrator.";
        user.ReviewedAt = DateTime.UtcNow;
        user.ReviewedBy = User?.Identity?.Name ?? "Admin";
        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User {Email} rejected by {Admin}. Reason: {Reason}", user.Email, User?.Identity?.Name, user.RejectionReason);
        if (TempData != null)
        {
            TempData["Warning"] = $"User {user.FullName} ({user.Email}) registration has been rejected.";
        }

        return RedirectToAction(nameof(Index), new { filter = "pending" });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Route("User/ToggleActive")]
    public async Task<IActionResult> ToggleActive(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return NotFound();

        // Prevent admin from deactivating themselves
        if (user.UserName == User?.Identity?.Name)
        {
            if (TempData != null)
            {
                TempData["Error"] = "You cannot deactivate your own administrator account.";
            }
            return RedirectToAction(nameof(Index), new { filter = "all" });
        }

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        if (TempData != null)
        {
            TempData["Success"] = $"User {user.FullName} is now {(user.IsActive ? "Active" : "Deactivated")}.";
        }
        return RedirectToAction(nameof(Index));
    }
}