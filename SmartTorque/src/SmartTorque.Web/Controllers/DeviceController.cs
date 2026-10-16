using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

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
    private readonly AppDbContext _db;
    private readonly IHubContext<LiveReadingHub> _hubContext;

    public DeviceController(
        IDeviceService deviceService,
        AppDbContext db,
        IHubContext<LiveReadingHub> hubContext)
    {
        _deviceService = deviceService;
        _db = db;
        _hubContext = hubContext;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] InstrumentType? instrument = null, [FromQuery] bool? activeOnly = null)
    {
        ViewData["ActiveNav"] = "Devices";

        var devices = await _deviceService.ListAsync(instrument, activeOnly);

        var offlineSecSetting = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "DeviceOfflineSeconds");
        int offlineSeconds = (offlineSecSetting != null && int.TryParse(offlineSecSetting.Value, out var sec)) ? sec : 30;

        var threshold = DateTime.UtcNow.AddSeconds(-offlineSeconds);

        var viewModels = devices.Select(d => new DeviceListItemViewModel
        {
            DeviceId = d.DeviceId,
            TransmitterId = d.TransmitterId,
            Name = d.Name,
            InstrumentType = d.InstrumentType,
            Location = d.Location,
            FirmwareVersion = d.FirmwareVersion,
            LastSeenAt = d.LastSeenAt,
            IsOnline = d.LastSeenAt.HasValue && d.LastSeenAt.Value >= threshold,
            IsActive = d.IsActive,
            CalibrationDue = d.CalibrationDue
        }).ToList();

        ViewBag.SelectedInstrument = instrument;
        ViewBag.ActiveOnly = activeOnly;
        ViewBag.OfflineSeconds = offlineSeconds;

        return View(viewModels);
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
}