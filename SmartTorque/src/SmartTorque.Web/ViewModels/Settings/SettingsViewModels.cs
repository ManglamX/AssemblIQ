using System.ComponentModel.DataAnnotations;

namespace SmartTorque.Web.ViewModels.Settings;

/// <summary>
/// The /Settings admin form. Editable fields are kept as raw strings so an
/// invalid POST re-renders without losing the admin's input (Rules §7.7).
/// Read-only fields are hydrated from the store for transparency only.
/// </summary>
public class SettingsIndexViewModel
{
    [Display(Name = "Device offline threshold (seconds)")]
    public string DeviceOfflineSeconds { get; set; } = "30";

    [Display(Name = "Image size limit (MB)")]
    public string ImageMaxMb { get; set; } = "5";

    [Display(Name = "Default step max retries")]
    public string MaxRetriesDefault { get; set; } = "3";

    [Display(Name = "BM column headings (JSON)")]
    public string BmHeaderMap { get; set; } = "";

    public string StableHoldMs { get; set; } = "800";

    public string SessionIdleTimeoutMin { get; set; } = "60";

    public string BmColourMatchTolerance { get; set; } = "40";

    public string RequireManualEntryApproval { get; set; } = "true";
}