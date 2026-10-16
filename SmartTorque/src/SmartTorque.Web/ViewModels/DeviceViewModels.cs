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
}

public class CreateDeviceViewModel
{
    [Required(ErrorMessage = "Transmitter ID is required.")]
    [StringLength(50, ErrorMessage = "Transmitter ID cannot exceed 50 characters.")]
    [Display(Name = "Transmitter ID (Hardware Identifier)")]
    public string TransmitterId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Device Name is required.")]
    [StringLength(100, ErrorMessage = "Device Name cannot exceed 100 characters.")]
    [Display(Name = "Device Friendly Name")]
    public string Name { get; set; } = string.Empty;

    [Required]
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