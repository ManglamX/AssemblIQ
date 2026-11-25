using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// Configurable BLE GATT &amp; frame-parsing protocol profile so any Bluetooth
/// torque wrench or vernier caliper can be mapped to the cockpit's normalized reading contract.
/// </summary>
public class BleProtocol
{
    public int BleProtocolId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Null = Universal (works for both Torque Wrench and Vernier Caliper),
    /// otherwise restricted/tagged for Torque or Vernier.
    /// </summary>
    public InstrumentType? SupportedInstrumentType { get; set; }

    // --- GATT Discovery & Characteristics ---
    public string ServiceUuid { get; set; } = string.Empty;

    public string TxCharUuid { get; set; } = string.Empty;

    public string? RxCharUuid { get; set; }

    /// <summary>Comma-separated BLE advertised name prefixes (e.g. "CEM3,Tohnichi,ESP,AssemblIQ").</summary>
    public string? DeviceNamePrefixes { get; set; }

    // --- Frame Parsing ---
    /// <summary>"Delimited", "Regex", "Json", or "HexBytes".</summary>
    public string FrameFormat { get; set; } = "Delimited";

    /// <summary>Only parse incoming frames starting with this prefix (e.g. "RE,").</summary>
    public string? FrameStartFilter { get; set; }

    /// <summary>Ignore incoming ACK/status frames starting with this prefix (e.g. "RE003").</summary>
    public string? IgnorePrefix { get; set; }

    /// <summary>Token delimiter when FrameFormat is "Delimited" (e.g. ",").</summary>
    public string? Delimiter { get; set; } = ",";

    /// <summary>Regex pattern when FrameFormat is "Regex".</summary>
    public string? RegexPattern { get; set; }

    // --- Cockpit Field Mapping ---
    /// <summary>Token index (Delimited), capture group (Regex), JSON key/path (Json), or byte spec (HexBytes) for the measurement value.</summary>
    public string ValueField { get; set; } = "2";

    /// <summary>Scale multiplier applied to the parsed numeric value (default 1.0).</summary>
    public double ValueMultiplier { get; set; } = 1.0;

    /// <summary>Mapping for DeviceSeq / counter.</summary>
    public string? CounterField { get; set; }

    /// <summary>Mapping for TransmitterId / toolSerial.</summary>
    public string? ToolSerialField { get; set; }

    /// <summary>Mapping for unit string (e.g. "nm", "mm").</summary>
    public string? UnitField { get; set; }

    /// <summary>Mapping for angle (optional, used in double-tightening warning).</summary>
    public string? AngleField { get; set; }

    /// <summary>Mapping for tool judgment code (e.g. "OO", "OD").</summary>
    public string? JudgmentField { get; set; }

    /// <summary>Judgment value indicating pass (e.g. "OO").</summary>
    public string? PassValue { get; set; }

    /// <summary>Substring/value in judgment indicating double-tightening (e.g. "D" or "OD").</summary>
    public string? DoubleTighteningValue { get; set; }

    /// <summary>Optional write command template sent to RxCharUuid on step load (e.g. "AT037,{HIGH},{LOW}\r\n").</summary>
    public string? SyncLimitsCommandTemplate { get; set; }

    public bool IsBuiltIn { get; set; } = false;

    public bool IsActive { get; set; } = true;

    /// <summary>Owning department (null = built-in system baseline; non-null = department-isolated profile).</summary>
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public ICollection<Device> Devices { get; set; } = new List<Device>();
}
