using System.Globalization;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

using SmartTorque.Core.Interfaces;
using SmartTorque.Services;
using SmartTorque.Web.ViewModels.Settings;

namespace SmartTorque.Web.Controllers;

/// <summary>
/// Admin settings page /Settings. GET hydrates every key (editable + read-only);
/// POST parses the editable strings and delegates validation, persistence and
/// the audit row to SettingsService.
/// </summary>
[Authorize(Roles = "Admin")]
public class SettingsController : Controller
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SettingsController> _logger;

    public SettingsController(ISettingsService settings, ILogger<SettingsController> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var model = await HydrateAsync(new SettingsIndexViewModel(), includeEditable: true, ct);
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "Settings";
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Index(SettingsIndexViewModel model, CancellationToken ct)
    {
        if (!int.TryParse(model.DeviceOfflineSeconds, NumberStyles.Integer, CultureInfo.InvariantCulture, out var deviceOfflineSeconds))
            ModelState.AddModelError(nameof(model.DeviceOfflineSeconds), "Device offline threshold must be a whole number.");

        if (!int.TryParse(model.ImageMaxMb, NumberStyles.Integer, CultureInfo.InvariantCulture, out var imageMaxMb))
            ModelState.AddModelError(nameof(model.ImageMaxMb), "Image size limit must be a whole number.");

        if (!int.TryParse(model.MaxRetriesDefault, NumberStyles.Integer, CultureInfo.InvariantCulture, out var maxRetriesDefault))
            ModelState.AddModelError(nameof(model.MaxRetriesDefault), "Default max retries must be a whole number.");

        if (!ModelState.IsValid)
        {
            page();
            model = await HydrateAsync(model, includeEditable: false, ct);
            return View(model);
        }

        var changedBy = User.Identity?.Name ?? "system";
        try
        {
            var updated = await _settings.SaveApplicationSettingsAsync(
                new ApplicationSettingsSaveRequest(
                    DeviceOfflineSeconds: deviceOfflineSeconds,
                    ImageMaxMb: imageMaxMb,
                    MaxRetriesDefault: maxRetriesDefault,
                    BmHeaderMap: model.BmHeaderMap),
                changedBy,
                ct);

            TempData["Success"] = updated == 0
                ? "No changes to save."
                : $"Settings saved — {updated} value(s) updated.";
            return RedirectToAction(nameof(Index));
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Settings validation failed for {User}", changedBy);
            ModelState.AddModelError(string.Empty, ex.Message);
            page();
            model = await HydrateAsync(model, includeEditable: false, ct);
            return View(model);
        }
    }

    private void page()
    {
        ViewData["Title"] = "Settings";
        ViewData["ActiveNav"] = "Settings";
    }

    /// <summary>Loads store values into the model. Read-only values are always refreshed; editable ones only on GET.</summary>
    private async Task<SettingsIndexViewModel> HydrateAsync(SettingsIndexViewModel model, bool includeEditable, CancellationToken ct)
    {
        var rows = await _settings.GetOverviewAsync(
        [
            SettingsService.KeyDeviceOfflineSeconds,
            SettingsService.KeyImageMaxMb,
            SettingsService.KeyMaxRetriesDefault,
            SettingsService.KeyStableHoldMs,
            SettingsService.KeySessionIdleTimeoutMin,
            SettingsService.KeyBmColourMatchTolerance,
            SettingsService.KeyRequireManualEntryApproval
        ], ct);

        foreach (var row in rows)
        {
            switch (row.Key)
            {
                case SettingsService.KeyDeviceOfflineSeconds when includeEditable:
                    model.DeviceOfflineSeconds = row.Value;
                    break;
                case SettingsService.KeyImageMaxMb when includeEditable:
                    model.ImageMaxMb = row.Value;
                    break;
                case SettingsService.KeyMaxRetriesDefault when includeEditable:
                    model.MaxRetriesDefault = row.Value;
                    break;
                case SettingsService.KeyStableHoldMs:
                    model.StableHoldMs = row.Value;
                    break;
                case SettingsService.KeySessionIdleTimeoutMin:
                    model.SessionIdleTimeoutMin = row.Value;
                    break;
                case SettingsService.KeyBmColourMatchTolerance:
                    model.BmColourMatchTolerance = row.Value;
                    break;
                case SettingsService.KeyRequireManualEntryApproval:
                    model.RequireManualEntryApproval = row.Value;
                    break;
            }
        }

        if (includeEditable)
        {
            model.BmHeaderMap = await _settings.GetAsync(SettingsService.KeyBmHeaderMap, ct) ?? "";
        }

        return model;
    }
}