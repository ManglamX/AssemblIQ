using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SmartTorque.Core.Entities;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.ApiKeys;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
public class ApiKeysController : Controller
{
    private readonly IApiKeyDirectoryService _apiKeyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(
        IApiKeyDirectoryService apiKeyService,
        UserManager<ApplicationUser> userManager,
        ILogger<ApiKeysController> logger)
    {
        _apiKeyService = apiKeyService;
        _userManager = userManager;
        _logger = logger;
    }

    [HttpGet]
    [Route("Admin/ApiKeys")]
    public async Task<IActionResult> Index()
    {
        ViewData["ActiveNav"] = "ApiKeys";

        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        var isMainAdmin = await _apiKeyService.IsMainAdminAsync(user.Id);

        var vm = new ApiKeysDashboardViewModel
        {
            IsMainAdmin = isMainAdmin,
            CurrentUserEmail = user.Email ?? user.UserName ?? "Admin"
        };

        if (isMainAdmin)
        {
            vm.AllKeys = await _apiKeyService.GetAllKeysForMainAdminAsync();
            vm.PendingRequests = await _apiKeyService.GetPendingRequestsAsync();
            vm.AllGrants = await _apiKeyService.GetAllGrantsAsync();
        }
        else
        {
            vm.ApprovedGrants = await _apiKeyService.GetApprovedGrantsForUserAsync(user.Id);
            vm.MyRequests = await _apiKeyService.GetRequestsForUserAsync(user.Id);
            vm.AvailableKeys = await _apiKeyService.GetAvailableKeysForUserAsync(user.Id);
        }

        return View(vm);
    }

    [HttpPost]
    [Route("Admin/ApiKeys/Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateApiKeyInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _apiKeyService.IsMainAdminAsync(user.Id))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please fill in all required fields to create an API key.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _apiKeyService.CreateKeyAsync(
                model.Name,
                model.Scope,
                model.Description,
                model.RawApiKey,
                user.Email ?? user.UserName ?? "MainAdmin");

            TempData["Success"] = $"API Key '{model.Name}' created successfully in directory.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create API key");
            TempData["Error"] = $"Failed to create API key: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/ApiKeys/Toggle/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Toggle(int id)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _apiKeyService.IsMainAdminAsync(user.Id))
        {
            return Forbid();
        }

        var isActive = await _apiKeyService.ToggleKeyStatusAsync(id);
        TempData["Success"] = $"API key is now {(isActive ? "Active" : "Disabled")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/ApiKeys/RequestAccess")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestAccess(RequestApiKeyInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Challenge();
        }

        if (!ModelState.IsValid)
        {
            TempData["Error"] = "Please select an API key and provide a reason for access.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            await _apiKeyService.RequestAccessAsync(model.ApiKeyId, user.Id, model.Reason);
            TempData["Success"] = "Your access request has been submitted to the Main Admin for review.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request API key access");
            TempData["Error"] = $"Failed to request access: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/ApiKeys/Approve/{grantId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int grantId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _apiKeyService.IsMainAdminAsync(user.Id))
        {
            return Forbid();
        }

        try
        {
            var grant = await _apiKeyService.ApproveRequestAsync(grantId, user.Id);
            TempData["Success"] = $"Approved API key access for user '{grant.User.Email ?? grant.User.UserName}'. Key is now accessible to them.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to approve API key request");
            TempData["Error"] = $"Failed to approve request: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/ApiKeys/Reject")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(RejectApiKeyInputModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _apiKeyService.IsMainAdminAsync(user.Id))
        {
            return Forbid();
        }

        try
        {
            await _apiKeyService.RejectRequestAsync(model.GrantId, user.Id, model.RejectionReason);
            TempData["Success"] = "Access request was rejected.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reject API key request");
            TempData["Error"] = $"Failed to reject request: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [Route("Admin/ApiKeys/Revoke/{grantId:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke(int grantId)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null || !await _apiKeyService.IsMainAdminAsync(user.Id))
        {
            return Forbid();
        }

        try
        {
            await _apiKeyService.RevokeAccessAsync(grantId, user.Id);
            TempData["Success"] = "API Key access grant revoked.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to revoke API key grant");
            TempData["Error"] = $"Failed to revoke access: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }
}
