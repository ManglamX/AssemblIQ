using System.ComponentModel.DataAnnotations;

using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels;

public class DeviceListItemViewModel
{
    public int DeviceId { get; set; }
    public string TransmitterId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public InstrumentType InstrumentType { get; set; }
    public string? Location { get; set; }
    public string? FirmwareVersion { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public bool IsOnline { get; set; }
    public bool IsActive { get; set; }
    public DateOnly? CalibrationDue { get; set; }
    public bool IsCalibrationOverdue => CalibrationDue.HasValue && CalibrationDue.Value < DateOnly.FromDateTime(DateTime.UtcNow);

    // Merged API Key and Access Details
    public int? ApiKeyId { get; set; }
    public string? RawApiKey { get; set; }
    public string? ApiKeyHash { get; set; }
    public bool HasKey => !string.IsNullOrEmpty(RawApiKey) || !string.IsNullOrEmpty(ApiKeyHash);
    public int ActiveGrantsCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class DeviceManagementIndexViewModel
{
    public List<DeviceListItemViewModel> Devices { get; set; } = new();
    public InstrumentType? SelectedInstrument { get; set; }
    public bool? ActiveOnly { get; set; }
    public int OfflineSeconds { get; set; } = 30;
    public string ActiveTab { get; set; } = "fleet";

    // Multi-Admin Access Governance Properties
    public bool IsMainAdmin { get; set; }
    public string CurrentUserEmail { get; set; } = string.Empty;
    public IReadOnlyList<SmartTorque.Core.Entities.ApiKeyAccessGrant> PendingRequests { get; set; } = [];
    public IReadOnlyList<SmartTorque.Core.Entities.ApiKeyAccessGrant> AllGrants { get; set; } = [];
    public IReadOnlyList<SmartTorque.Core.Entities.ApiKeyAccessGrant> ApprovedGrants { get; set; } = [];
    public IReadOnlyList<SmartTorque.Core.Entities.ApiKeyAccessGrant> MyRequests { get; set; } = [];
    public IReadOnlyList<SmartTorque.Core.Entities.ApiKeyDirectoryEntry> AvailableKeys { get; set; } = [];

    public int TotalCount => Devices.Count;
    public int OnlineCount => Devices.Count(d => d.IsOnline && d.IsActive);
    public int TorqueCount => Devices.Count(d => d.InstrumentType == InstrumentType.Torque);
    public int VernierCount => Devices.Count(d => d.InstrumentType == InstrumentType.Vernier);
    public int PendingRequestsCount => PendingRequests.Count;
}

public class CreateDeviceViewModel
{
    /// <summary>Device category chosen at top of form: "Node", "TorqueWrench", or "VernierCaliper"</summary>
    [Required(ErrorMessage = "Please select a device category.")]
    public string DeviceCategory { get; set; } = "Node";

    // --- Node fields ---
    [StringLength(50, ErrorMessage = "Transmitter ID cannot exceed 50 characters.")]
    [Display(Name = "Transmitter ID (Hardware Identifier)")]
    public string? TransmitterId { get; set; }

    [Required(ErrorMessage = "Device Name is required.")]
    [StringLength(100, ErrorMessage = "Device Name cannot exceed 100 characters.")]
    [Display(Name = "Device Friendly Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Instrument Type")]
    public InstrumentType InstrumentType { get; set; } = InstrumentType.Torque;

    [StringLength(100)]
    [Display(Name = "Physical Location / Workstation")]
    public string? Location { get; set; }

    [StringLength(50)]
    [Display(Name = "Firmware Version")]
    public string? FirmwareVersion { get; set; }

    [Display(Name = "Calibration Due Date")]
    public DateOnly? CalibrationDue { get; set; }

    // --- BLE fields (for TorqueWrench / VernierCaliper) ---
    [StringLength(36)]
    [Display(Name = "BLE UUID")]
    public string? BleUuid { get; set; }

    public bool IsBluetoothDevice { get; set; } = false;
}

public class EditDeviceViewModel
{
    public int DeviceId { get; set; }

    [Display(Name = "Transmitter ID")]
    public string TransmitterId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Device Name is required.")]
    [StringLength(100)]
    [Display(Name = "Device Friendly Name")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Instrument Type")]
    public InstrumentType InstrumentType { get; set; }

    [StringLength(100)]
    [Display(Name = "Physical Location / Workstation")]
    public string? Location { get; set; }

    [Display(Name = "Calibration Due Date")]
    public DateOnly? CalibrationDue { get; set; }
}