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

    // Bluetooth & Category Details
    public bool IsBluetoothDevice { get; set; }
    public string? BleUuid { get; set; }
    public int? BleProtocolId { get; set; }
    public string? BleProtocolName { get; set; }
    public string DeviceCategory => IsBluetoothDevice
        ? (InstrumentType == InstrumentType.Vernier ? "VernierCaliper" : "TorqueWrench")
        : "Node";
    public string DeviceCategoryDisplay => IsBluetoothDevice
        ? (InstrumentType == InstrumentType.Vernier ? "Vernier Caliper (BLE)" : "Torque Wrench (BLE)")
        : "Node (ESP32 Gateway)";

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
    public string? SelectedCategory { get; set; } // "Node", "TorqueWrench", "VernierCaliper"
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
    public IReadOnlyList<SmartTorque.Core.Entities.BleProtocol> BleProtocols { get; set; } = [];

    // Fleet Summary Aggregates across all categories
    public int TotalCount { get; set; }
    public int OnlineCount { get; set; }
    public int NodeCount { get; set; }
    public int BleTorqueCount { get; set; }
    public int BleVernierCount { get; set; }
    public int TorqueCount { get; set; }
    public int VernierCount { get; set; }
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
    [StringLength(64)]
    [Display(Name = "BLE UUID")]
    public string? BleUuid { get; set; }

    [Display(Name = "BLE Protocol Mapping")]
    public int? BleProtocolId { get; set; }

    public bool IsBluetoothDevice { get; set; } = false;

    public List<SmartTorque.Core.Entities.BleProtocol> AvailableProtocols { get; set; } = new();
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

    public bool IsBluetoothDevice { get; set; }

    [StringLength(64)]
    [Display(Name = "BLE Service UUID")]
    public string? BleUuid { get; set; }

    [Display(Name = "BLE Protocol Mapping")]
    public int? BleProtocolId { get; set; }

    public List<SmartTorque.Core.Entities.BleProtocol> AvailableProtocols { get; set; } = new();
}