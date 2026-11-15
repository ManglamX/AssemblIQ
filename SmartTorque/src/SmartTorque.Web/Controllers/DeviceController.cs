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
using SmartTorque.Web.ViewModels;

namespace SmartTorque.Web.Controllers;

[Authorize(Roles = "Admin")]
[Route("Device")]
public class DeviceController : Controller
{
    private readonly IDeviceService _deviceService;
    private readonly IApiKeyDirectoryService _apiKeyService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly AppDbContext _db;
    private readonly IHubContext<LiveReadingHub> _hubContext;
    private readonly IQrCodeService _qrCodeService;

    public DeviceController(
        IDeviceService deviceService,
        IApiKeyDirectoryService apiKeyService,
        UserManager<ApplicationUser> userManager,
        AppDbContext db,
        IHubContext<LiveReadingHub> hubContext,
        IQrCodeService? qrCodeService = null)
    {
        _deviceService = deviceService;
        _apiKeyService = apiKeyService;
        _userManager = userManager;
        _db = db;
        _hubContext = hubContext;
        _qrCodeService = qrCodeService ?? new QrCodeService();
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] InstrumentType? instrument = null, [FromQuery] bool? activeOnly = null, [FromQuery] string tab = "fleet")
    {
        ViewData["ActiveNav"] = "Devices";

        var user = await _userManager.GetUserAsync(User);
        bool isMainAdmin = user != null && await _apiKeyService.IsMainAdminAsync(user.Id);

        var devices = await _deviceService.ListAsync(instrument, activeOnly);

        // Fetch all registered API keys to correlate with hardware devices
        var allKeyEntries = await _db.ApiKeyDirectoryEntries
            .AsNoTracking()
            .Include(k => k.Grants)
                .ThenInclude(g => g.User)
            .ToListAsync();

        var keyDictByScope = allKeyEntries
            .Where(k => !string.IsNullOrWhiteSpace(k.ApiScope))
            .GroupBy(k => k.ApiScope!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        var keyDictByName = allKeyEntries
            .Where(k => !string.IsNullOrWhiteSpace(k.KeyName))
            .GroupBy(k => k.KeyName!.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(k => k.CreatedAt).First(), StringComparer.OrdinalIgnoreCase);

        var offlineSecSetting = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "DeviceOfflineSeconds");
        int offlineSeconds = (offlineSecSetting != null && int.TryParse(offlineSecSetting.Value, out var sec)) ? sec : 30;

        var threshold = DateTime.UtcNow.AddSeconds(-offlineSeconds);

        var deviceViewModels = devices.Select(d =>
        {
            var txTrim = d.TransmitterId?.Trim() ?? "";
            var nameTrim = d.Name?.Trim() ?? "";

            ApiKeyDirectoryEntry? matchedKey = null;
            if (!string.IsNullOrEmpty(txTrim))
            {
                keyDictByScope.TryGetValue(txTrim, out matchedKey);
            }
            if (matchedKey == null && !string.IsNullOrEmpty(nameTrim))
            {
                keyDictByName.TryGetValue(nameTrim, out matchedKey);
            }

            return new DeviceListItemViewModel
            {
                DeviceId = d.DeviceId,
                TransmitterId = d.TransmitterId ?? string.Empty,
                Name = d.Name ?? string.Empty,
                InstrumentType = d.InstrumentType,
                Location = d.Location,
                FirmwareVersion = d.FirmwareVersion,
                LastSeenAt = d.LastSeenAt,
                IsOnline = d.LastSeenAt.HasValue && d.LastSeenAt.Value >= threshold,
                IsActive = d.IsActive,
                CalibrationDue = d.CalibrationDue,
                ApiKeyId = matchedKey?.ApiKeyId,
                RawApiKey = matchedKey?.RawApiKey,
                ApiKeyHash = d.ApiKeyHash,
                ActiveGrantsCount = matchedKey?.Grants?.Count(g => g.Status == ApiKeyRequestStatus.Approved) ?? 0,
                CreatedAt = d.CreatedAt
            };
        }).ToList();

        var vm = new DeviceManagementIndexViewModel
        {
            Devices = deviceViewModels,
            SelectedInstrument = instrument,
            ActiveOnly = activeOnly,
            OfflineSeconds = offlineSeconds,
            ActiveTab = tab ?? "fleet",
            IsMainAdmin = isMainAdmin,
            CurrentUserEmail = user?.Email ?? user?.UserName ?? "Admin"
        };

        if (isMainAdmin)
        {
            vm.PendingRequests = await _apiKeyService.GetPendingRequestsAsync();
            vm.AllGrants = await _apiKeyService.GetAllGrantsAsync();
        }
        else if (user != null)
        {
            vm.ApprovedGrants = await _apiKeyService.GetApprovedGrantsForUserAsync(user.Id);
            vm.MyRequests = await _apiKeyService.GetRequestsForUserAsync(user.Id);
            vm.AvailableKeys = await _apiKeyService.GetAvailableKeysForUserAsync(user.Id);
        }

        ViewBag.SelectedInstrument = instrument;
        ViewBag.ActiveOnly = activeOnly;
        ViewBag.OfflineSeconds = offlineSeconds;

        return View(vm);
    }

    [HttpGet("Create")]
    public IActionResult Create()
    {
        ViewData["ActiveNav"] = "Devices";
        return View(new CreateDeviceViewModel());
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceViewModel model)
    {
        ViewData["ActiveNav"] = "Devices";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var request = new RegisterDeviceRequest(
                TransmitterId: model.TransmitterId,
                Name: model.Name,
                InstrumentType: model.InstrumentType,
                Location: model.Location,
                FirmwareVersion: model.FirmwareVersion,
                CalibrationDue: model.CalibrationDue
            );

            var result = await _deviceService.RegisterAsync(request, User.Identity?.Name ?? "admin");

            TempData["NewApiKey"] = result.RawApiKey;
            TempData["NewDeviceName"] = result.Device.Name;
            TempData["NewDeviceTransmitter"] = result.Device.TransmitterId;
            TempData["NewDeviceId"] = result.Device.DeviceId;
            TempData["NewDeviceInstrument"] = result.Device.InstrumentType.ToString();
            TempData["Success"] = $"Device '{result.Device.Name}' registered successfully.";

            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(nameof(model.TransmitterId), ex.Message);
            return View(model);
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Registration failed: {ex.Message}");
            return View(model);
        }
    }

    [HttpGet("Edit/{id:int}")]
    public async Task<IActionResult> Edit(int id)
    {
        ViewData["ActiveNav"] = "Devices";

        var device = await _deviceService.GetByIdAsync(id);
        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction(nameof(Index));
        }

        var model = new EditDeviceViewModel
        {
            DeviceId = device.DeviceId,
            TransmitterId = device.TransmitterId,
            Name = device.Name,
            InstrumentType = device.InstrumentType,
            Location = device.Location,
            CalibrationDue = device.CalibrationDue
        };

        return View(model);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditDeviceViewModel model)
    {
        ViewData["ActiveNav"] = "Devices";

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _deviceService.UpdateAsync(id, model.Name, model.Location, model.CalibrationDue);
            TempData["Success"] = $"Device '{model.Name}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            ModelState.AddModelError(string.Empty, $"Update failed: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost("ReissueKey/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ReissueKey(int id)
    {
        try
        {
            var result = await _deviceService.ReissueApiKeyAsync(id);
            TempData["NewApiKey"] = result.RawApiKey;
            TempData["NewDeviceName"] = result.Device.Name;
            TempData["NewDeviceTransmitter"] = result.Device.TransmitterId;
            TempData["NewDeviceId"] = result.Device.DeviceId;
            TempData["NewDeviceInstrument"] = result.Device.InstrumentType.ToString();
            TempData["Success"] = $"API key reissued for '{result.Device.Name}'.";
        }
        catch (Exception ex)
        {
            TempData["Error"] = $"Failed to reissue API key: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost("ToggleActive/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var device = await _deviceService.GetByIdAsync(id);
        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction(nameof(Index));
        }

        bool newStatus = !device.IsActive;
        await _deviceService.SetActiveAsync(id, newStatus);

        await _hubContext.Clients.Group("admins").SendAsync("deviceStatus", new
        {
            transmitterId = device.TransmitterId,
            online = newStatus && device.LastSeenAt.HasValue && device.LastSeenAt.Value >= DateTime.UtcNow.AddSeconds(-30),
            isActive = newStatus
        });

        TempData["Success"] = $"Device '{device.Name}' is now {(newStatus ? "Active" : "Inactive")}.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("Delete/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _deviceService.DeleteAsync(id);
        if (!result.Success)
        {
            TempData["Error"] = result.Message;
        }
        else if (!result.WasHardDeleted)
        {
            TempData["Warning"] = result.Message;
        }
        else
        {
            TempData["Success"] = result.Message;
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet("QrCode/{id:int}")]
    [AllowAnonymous]
    public async Task<IActionResult> QrCode(int id, [FromQuery] int? pixels = 10)
    {
        var device = await _deviceService.GetByIdAsync(id);
        if (device == null) return NotFound("Device not found.");

        var entry = await _db.ApiKeyDirectoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.ApiScope == device.TransmitterId);

        var key = entry?.RawApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            key = device.TransmitterId;
        }

        var svg = _qrCodeService.GenerateSvg(key, pixels ?? 10);
        return Content(svg, "image/svg+xml");
    }

    [HttpGet("RawKeyQr")]
    [AllowAnonymous]
    public IActionResult RawKeyQr([FromQuery] string key, [FromQuery] int? pixels = 10)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest("Key is required.");
        var svg = _qrCodeService.GenerateSvg(key.Trim(), pixels ?? 10);
        return Content(svg, "image/svg+xml");
    }

    [HttpGet("PrintQr/{id:int}")]
    public async Task<IActionResult> PrintQr(int id)
    {
        var device = await _deviceService.GetByIdAsync(id);
        if (device == null)
        {
            TempData["Error"] = "Device not found.";
            return RedirectToAction(nameof(Index));
        }

        var entry = await _db.ApiKeyDirectoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.ApiScope == device.TransmitterId);

        var rawApiKey = entry?.RawApiKey ?? device.TransmitterId;

        ViewBag.Device = device;
        ViewBag.RawApiKey = rawApiKey;
        ViewBag.QrSvg = _qrCodeService.GenerateSvg(rawApiKey, 12);

        return View(device);
    }
}
