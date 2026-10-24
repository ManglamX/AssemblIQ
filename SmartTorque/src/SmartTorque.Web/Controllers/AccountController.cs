using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Web.ViewModels.Account;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// Handles authentication, registration, access status, and logout.
/// </summary>
[AllowAnonymous]
public class AccountController : Controller
{
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<AccountController> _logger;

    public AccountController(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager,
        ILogger<AccountController> logger)
    {
        _signInManager = signInManager;
        _userManager = userManager;
        _roleManager = roleManager;
        _logger = logger;
    }

    // GET /Account/Login
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToRoleHome();

        ViewData["ReturnUrl"] = returnUrl;
        return View();
    }

    // POST /Account/Login
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;

        if (!ModelState.IsValid)
            return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user == null)
        {
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
        {
            // Triggers lockout count if configured
            await _signInManager.PasswordSignInAsync(model.Email, model.Password, isPersistent: false, lockoutOnFailure: true);
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        // Check registration approval status
        if (user.RegistrationStatus == RegistrationStatus.Pending)
        {
            return RedirectToAction(nameof(AccessStatus), new { email = user.Email });
        }

        if (user.RegistrationStatus == RegistrationStatus.Rejected)
        {
            return RedirectToAction(nameof(AccessStatus), new { email = user.Email });
        }

        if (!user.IsActive)
        {
            ModelState.AddModelError(string.Empty, "Your account has been deactivated. Please contact your system administrator.");
            return View(model);
        }

        await _signInManager.SignInAsync(user, isPersistent: false);
        _logger.LogInformation("User {Email} logged in successfully", model.Email);

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && returnUrl != "/" && !returnUrl.Equals("/Home", StringComparison.OrdinalIgnoreCase) && !returnUrl.Equals("/Home/Index", StringComparison.OrdinalIgnoreCase))
            return Redirect(returnUrl);

        var roles = await _userManager.GetRolesAsync(user);
        if (roles.Contains("Admin"))
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

        if (roles.Contains("Worker"))
            return RedirectToAction("Index", "JobBoard", new { area = "Worker" });

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }

    // GET /Account/Register
    [HttpGet]
    public IActionResult Register()
    {
        if (_signInManager.IsSignedIn(User))
            return RedirectToRoleHome();

        return View(new RegisterViewModel());
    }

    // POST /Account/Register
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterViewModel model)
    {
        if (!ModelState.IsValid)
            return View(model);

        var normalizedEmail = model.Email.Trim().ToLowerInvariant();
        var existing = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existing != null)
        {
            ModelState.AddModelError(nameof(model.Email), "An account with this email address is already registered.");
            return View(model);
        }

        var requestedRole = (model.RequestedRole == "Admin") ? "Admin" : "Worker";

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = model.FullName.Trim(),
            EmployeeNo = model.EmployeeNo?.Trim(),
            IsActive = false, // Disabled until admin approval
            RegistrationStatus = RegistrationStatus.Pending,
            RequestedRole = requestedRole,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }

        // Ensure role exists and associate
        if (!await _roleManager.RoleExistsAsync(requestedRole))
        {
            await _roleManager.CreateAsync(new IdentityRole(requestedRole));
        }
        await _userManager.AddToRoleAsync(user, requestedRole);

        _logger.LogInformation("New user registered: {Email} requesting role {Role}. Awaiting admin review.", normalizedEmail, requestedRole);

        return RedirectToAction(nameof(AccessStatus), new { email = normalizedEmail });
    }

    // GET /Account/AccessStatus
    [HttpGet]
    public async Task<IActionResult> AccessStatus(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return RedirectToAction(nameof(Login));

        var user = await _userManager.FindByEmailAsync(email.Trim());
        if (user == null)
            return RedirectToAction(nameof(Login));

        return View(user);
    }

    // GET or POST /Account/Logout
    [HttpGet]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        _logger.LogInformation("User logged out");
        return RedirectToAction(nameof(Login));
    }

    // GET /Account/AccessDenied
    [HttpGet]
    public IActionResult AccessDenied() => View();

    // ---------------------------------------------------------------
    // Helpers
    // ---------------------------------------------------------------

    private IActionResult RedirectToRoleHome()
    {
        if (User.IsInRole("Admin"))
            return RedirectToAction("Index", "Dashboard", new { area = "Admin" });

        if (User.IsInRole("Worker"))
            return RedirectToAction("Index", "JobBoard", new { area = "Worker" });

        return RedirectToAction("Index", "Dashboard", new { area = "Admin" });
    }
}