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
    public async Task<IActionResult> Index(
        [FromQuery] string? category = null,
        [FromQuery] InstrumentType? instrument = null,
        [FromQuery] bool? activeOnly = null,
        [FromQuery] string tab = "fleet")
    {
        ViewData["ActiveNav"] = "Devices";

        var user = (_userManager != null && User != null) ? await _userManager.GetUserAsync(User) : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);

        var allDevices = await _deviceService.ListAsync(null, activeOnly);

        if (!isMainAdmin && user?.DepartmentId != null)
        {
            var deptId = user.DepartmentId.Value;
            var dept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == deptId);

            var sessionDevices = await _db.Sessions
                .AsNoTracking()
                .Where(s => (s.Job.Product != null && s.Job.Product.DepartmentId == deptId) ||
                            (s.Job.AssemblyLine != null && s.Job.AssemblyLine.DepartmentId == deptId))
                .Select(s => new { s.TorqueDeviceId, s.VernierDeviceId })
                .ToListAsync();

            var deptDeviceIds = sessionDevices
                .SelectMany(s => new[] { s.TorqueDeviceId, s.VernierDeviceId })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .ToHashSet();

            // Strict department isolation:
            // 1. If device has DepartmentId set, it MUST match this department.
            // 2. If legacy device has DepartmentId == null, only match via this department's sessions or explicit Dept Name/Code in Location (never generic Station 1).
            allDevices = allDevices.Where(d =>
                d.DepartmentId.HasValue
                    ? d.DepartmentId.Value == deptId
                    : (deptDeviceIds.Contains(d.DeviceId) ||
                       (!string.IsNullOrWhiteSpace(d.Location) && (
                           (!string.IsNullOrEmpty(dept?.Name) && d.Location.Contains(dept.Name, StringComparison.OrdinalIgnoreCase)) ||
                           (!string.IsNullOrEmpty(dept?.Code) && d.Location.Contains(dept.Code, StringComparison.OrdinalIgnoreCase)))))
            ).ToList();
        }

        var offlineSecSetting = await _db.Settings
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Key == "DeviceOfflineSeconds");
        int offlineSeconds = (offlineSecSetting != null && int.TryParse(offlineSecSetting.Value, out var sec)) ? sec : 30;
        var threshold = DateTime.UtcNow.AddSeconds(-offlineSeconds);

        // Aggregates across entire fleet for KPI cards & category badges
        int totalCount = allDevices.Count;
        int onlineCount = allDevices.Count(d => d.IsActive && d.LastSeenAt.HasValue && d.LastSeenAt.Value >= threshold);
        int nodeCount = allDevices.Count(d => !d.IsBluetoothDevice);
        int bleTorqueCount = allDevices.Count(d => d.IsBluetoothDevice && d.InstrumentType == InstrumentType.Torque);
        int bleVernierCount = allDevices.Count(d => d.IsBluetoothDevice && d.InstrumentType == InstrumentType.Vernier);
        int torqueCount = allDevices.Count(d => d.InstrumentType == InstrumentType.Torque);
        int vernierCount = allDevices.Count(d => d.InstrumentType == InstrumentType.Vernier);

        // Apply Category or Instrument filter
        var filteredDevices = allDevices.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(category))
        {
            if (category.Equals("Node", StringComparison.OrdinalIgnoreCase))
            {
                filteredDevices = filteredDevices.Where(d => !d.IsBluetoothDevice);
            }
            else if (category.Equals("TorqueWrench", StringComparison.OrdinalIgnoreCase))
            {
                filteredDevices = filteredDevices.Where(d => d.IsBluetoothDevice && d.InstrumentType == InstrumentType.Torque);
            }
            else if (category.Equals("VernierCaliper", StringComparison.OrdinalIgnoreCase))
            {
                filteredDevices = filteredDevices.Where(d => d.IsBluetoothDevice && d.InstrumentType == InstrumentType.Vernier);
            }
        }
        else if (instrument.HasValue)
        {
            filteredDevices = filteredDevices.Where(d => d.InstrumentType == instrument.Value);
        }

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

        var protocolsQuery = _db.BleProtocols.AsNoTracking();
        if (!isMainAdmin && user?.DepartmentId != null)
        {
            var deptId = user.DepartmentId.Value;
            protocolsQuery = protocolsQuery.Where(p => p.DepartmentId == deptId);
        }
        var protocols = await protocolsQuery
            .OrderByDescending(p => p.DepartmentId != null)
            .ThenByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .ToListAsync();
        var protocolNameById = protocols.ToDictionary(p => p.BleProtocolId, p => p.Name);

        var deviceViewModels = filteredDevices.Select(d =>
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
                IsBluetoothDevice = d.IsBluetoothDevice,
                BleUuid = d.BleUuid,
                BleProtocolId = d.BleProtocolId,
                BleProtocolName = d.BleProtocolId.HasValue && protocolNameById.TryGetValue(d.BleProtocolId.Value, out var pName) ? pName : null,
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
            SelectedCategory = category,
            SelectedInstrument = instrument,
            ActiveOnly = activeOnly,
            OfflineSeconds = offlineSeconds,
            ActiveTab = tab ?? "fleet",
            IsMainAdmin = isMainAdmin,
            CurrentUserEmail = user?.Email ?? user?.UserName ?? "Admin",
            TotalCount = totalCount,
            OnlineCount = onlineCount,
            NodeCount = nodeCount,
            BleTorqueCount = bleTorqueCount,
            BleVernierCount = bleVernierCount,
            TorqueCount = torqueCount,
            VernierCount = vernierCount,
            BleProtocols = protocols
        };

        if (isMainAdmin && _apiKeyService != null)
        {
            vm.PendingRequests = await _apiKeyService.GetPendingRequestsAsync();
            vm.AllGrants = await _apiKeyService.GetAllGrantsAsync();
        }
        else if (user != null && _apiKeyService != null)
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

    private async Task<List<BleProtocol>> GetProtocolsForCurrentUserAsync()
    {
        var user = (_userManager != null && User != null) ? await _userManager.GetUserAsync(User) : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);
        var query = _db.BleProtocols.AsNoTracking().Where(p => p.IsActive);
        if (!isMainAdmin && user?.DepartmentId != null)
        {
            var deptId = user.DepartmentId.Value;
            query = query.Where(p => p.DepartmentId == deptId);
        }
        return await query
            .OrderByDescending(p => p.DepartmentId != null)
            .ThenByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .ToListAsync();
    }

    [HttpGet("Create")]
    public async Task<IActionResult> Create()
    {
        ViewData["ActiveNav"] = "Devices";
        var protocols = await GetProtocolsForCurrentUserAsync();
        var defaultTohnichi = protocols.FirstOrDefault(p => p.Name.Contains("Tohnichi"));
        return View(new CreateDeviceViewModel
        {
            BleProtocolId = defaultTohnichi?.BleProtocolId,
            AvailableProtocols = protocols
        });
    }

    [HttpPost("Create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateDeviceViewModel model)
    {
        ViewData["ActiveNav"] = "Devices";
        model.AvailableProtocols = await GetProtocolsForCurrentUserAsync();

        // Determine if BLE device
        bool isBle = model.DeviceCategory == "TorqueWrench" || model.DeviceCategory == "VernierCaliper";
        model.IsBluetoothDevice = isBle;

        // Set InstrumentType from category for BLE devices
        if (isBle)
        {
            model.InstrumentType = model.DeviceCategory == "VernierCaliper" ? InstrumentType.Vernier : InstrumentType.Torque;
            if (model.BleProtocolId.HasValue && string.IsNullOrWhiteSpace(model.BleUuid))
            {
                var proto = model.AvailableProtocols.FirstOrDefault(p => p.BleProtocolId == model.BleProtocolId.Value);
                if (proto != null && !string.IsNullOrWhiteSpace(proto.ServiceUuid))
                {
                    model.BleUuid = proto.ServiceUuid;
                }
            }
        }

        // Conditional validation
        if (!isBle && string.IsNullOrWhiteSpace(model.TransmitterId))
        {
            ModelState.AddModelError(nameof(model.TransmitterId), "Transmitter ID is required for Node devices.");
        }
        if (isBle && string.IsNullOrWhiteSpace(model.BleUuid))
        {
            ModelState.AddModelError(nameof(model.BleUuid), "BLE UUID is required for Bluetooth devices.");
        }

        // Clear unrelated field errors
        if (isBle) ModelState.Remove(nameof(model.TransmitterId));
        if (!isBle) ModelState.Remove(nameof(model.BleUuid));

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            // For BLE: use Name as transmitter ID (device name), store BleUuid
            var transmitterId = isBle ? (model.Name.ToUpper().Replace(" ", "-")) : model.TransmitterId!;
            var firmwareOrUuid = isBle ? model.BleUuid : model.FirmwareVersion;

            var request = new RegisterDeviceRequest(
                TransmitterId: transmitterId,
                Name: model.Name,
                InstrumentType: model.InstrumentType,
                Location: model.Location,
                FirmwareVersion: firmwareOrUuid,
                CalibrationDue: model.CalibrationDue
            );

            var result = await _deviceService.RegisterAsync(request, User.Identity?.Name ?? "admin");

            // Assign owning department + BLE fields
            var currentUser = _userManager != null ? await _userManager.GetUserAsync(User) : null;
            var device = await _db.Devices.FindAsync(result.Device.DeviceId);
            if (device != null)
            {
                if (currentUser?.DepartmentId != null)
                    device.DepartmentId = currentUser.DepartmentId;

                if (isBle)
                {
                    if (!string.IsNullOrWhiteSpace(model.BleUuid))
                        device.BleUuid = model.BleUuid.Trim().ToLowerInvariant();
                    device.IsBluetoothDevice = true;
                    device.BleProtocolId = model.BleProtocolId;
                }
                await _db.SaveChangesAsync();
            }

            TempData["NewApiKey"] = isBle ? null : result.RawApiKey;
            TempData["NewDeviceName"] = result.Device.Name;
            TempData["NewDeviceTransmitter"] = result.Device.TransmitterId;
            TempData["NewDeviceId"] = result.Device.DeviceId;
            TempData["NewDeviceInstrument"] = result.Device.InstrumentType.ToString();
            TempData["NewDeviceIsBle"] = isBle;
            TempData["NewDeviceBleUuid"] = model.BleUuid;
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

        var protocols = await _db.BleProtocols
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .ToListAsync();

        var model = new EditDeviceViewModel
        {
            DeviceId = device.DeviceId,
            TransmitterId = device.TransmitterId,
            Name = device.Name,
            InstrumentType = device.InstrumentType,
            Location = device.Location,
            CalibrationDue = device.CalibrationDue,
            IsBluetoothDevice = device.IsBluetoothDevice,
            BleUuid = device.BleUuid,
            BleProtocolId = device.BleProtocolId,
            AvailableProtocols = protocols
        };

        return View(model);
    }

    [HttpPost("Edit/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, EditDeviceViewModel model)
    {
        ViewData["ActiveNav"] = "Devices";
        model.AvailableProtocols = await _db.BleProtocols
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            await _deviceService.UpdateAsync(id, model.Name, model.Location, model.CalibrationDue);
            var dev = await _db.Devices.FindAsync(id);
            if (dev != null && dev.IsBluetoothDevice)
            {
                dev.BleProtocolId = model.BleProtocolId;
                if (!string.IsNullOrWhiteSpace(model.BleUuid))
                {
                    dev.BleUuid = model.BleUuid.Trim().ToLowerInvariant();
                }
                else if (model.BleProtocolId.HasValue)
                {
                    var proto = model.AvailableProtocols.FirstOrDefault(p => p.BleProtocolId == model.BleProtocolId.Value);
                    if (proto != null) dev.BleUuid = proto.ServiceUuid.Trim().ToLowerInvariant();
                }
                dev.UpdatedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();
            }
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
    public async Task<IActionResult> QrCode(int id, [FromQuery] int? pixels = 10)
    {
        var device = await _deviceService.GetByIdAsync(id);
        if (device == null) return NotFound("Device not found.");

        var user = (_userManager != null && User != null) ? await _userManager.GetUserAsync(User) : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);
        if (!isMainAdmin && user?.DepartmentId != null && device.DepartmentId.HasValue && device.DepartmentId.Value != user.DepartmentId.Value)
        {
            return Forbid();
        }

        var entry = await _db.ApiKeyDirectoryEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(k => k.ApiScope == device.TransmitterId);

        var key = entry?.RawApiKey;
        if (string.IsNullOrWhiteSpace(key))
        {
            key = device.TransmitterId;
        }

        var svg = _qrCodeService.GenerateSvg(key, Math.Clamp(pixels ?? 10, 1, 25));
        return Content(svg, "image/svg+xml");
    }

    [HttpGet("RawKeyQr")]
    public IActionResult RawKeyQr([FromQuery] string key, [FromQuery] int? pixels = 10)
    {
        if (string.IsNullOrWhiteSpace(key)) return BadRequest("Key is required.");
        var svg = _qrCodeService.GenerateSvg(key.Trim(), Math.Clamp(pixels ?? 10, 1, 25));
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

    // ── BLE PROTOCOL MAPPING ───────────────────────────────────────────────────

    [HttpPost("SaveProtocol")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProtocol(
        int? BleProtocolId,
        string Name,
        string? Description,
        int? SupportedInstrumentType,
        string ServiceUuid,
        string TxCharUuid,
        string? RxCharUuid,
        string? DeviceNamePrefixes,
        string FrameFormat,
        string? FrameStartFilter,
        string? IgnorePrefix,
        string? Delimiter,
        string? RegexPattern,
        string ValueField,
        double ValueMultiplier,
        string? CounterField,
        string? ToolSerialField,
        string? UnitField,
        string? AngleField,
        string? JudgmentField,
        string? PassValue,
        string? DoubleTighteningValue,
        string? SyncLimitsCommandTemplate)
    {
        if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(ServiceUuid) || string.IsNullOrWhiteSpace(TxCharUuid) || string.IsNullOrWhiteSpace(ValueField))
        {
            TempData["Error"] = "Protocol Name, Service UUID, TX Notify UUID, and Value Mapping Field are required.";
            return RedirectToAction(nameof(Index), new { tab = "protocols" });
        }

        InstrumentType? instType = SupportedInstrumentType.HasValue && Enum.IsDefined(typeof(InstrumentType), SupportedInstrumentType.Value)
            ? (InstrumentType)SupportedInstrumentType.Value
            : null;

        var user = (_userManager != null && User != null) ? await _userManager.GetUserAsync(User) : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);
        int? userDeptId = (!isMainAdmin && user?.DepartmentId != null) ? user.DepartmentId : null;

        BleProtocol? entity = null;
        if (BleProtocolId.HasValue && BleProtocolId.Value > 0)
        {
            entity = await _db.BleProtocols.FindAsync(BleProtocolId.Value);
            if (entity != null && userDeptId.HasValue)
            {
                // If a Dept Admin edits a shared built-in protocol, clone it into their department so other departments stay unaffected
                if (entity.IsBuiltIn && entity.DepartmentId == null)
                {
                    entity = null;
                }
                else if (entity.DepartmentId != userDeptId)
                {
                    TempData["Error"] = "You cannot modify a BLE protocol owned by another department.";
                    return RedirectToAction(nameof(Index), new { tab = "protocols" });
                }
            }
        }

        if (entity == null)
        {
            entity = new BleProtocol
            {
                CreatedAt = DateTime.UtcNow,
                IsActive = true,
                IsBuiltIn = false,
                DepartmentId = userDeptId
            };
            _db.BleProtocols.Add(entity);
        }
        else
        {
            entity.UpdatedAt = DateTime.UtcNow;
        }

        entity.Name = Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim();
        entity.SupportedInstrumentType = instType;
        entity.ServiceUuid = ServiceUuid.Trim().ToLowerInvariant();
        entity.TxCharUuid = TxCharUuid.Trim().ToLowerInvariant();
        entity.RxCharUuid = string.IsNullOrWhiteSpace(RxCharUuid) ? null : RxCharUuid.Trim().ToLowerInvariant();
        entity.DeviceNamePrefixes = string.IsNullOrWhiteSpace(DeviceNamePrefixes) ? null : DeviceNamePrefixes.Trim();
        entity.FrameFormat = string.IsNullOrWhiteSpace(FrameFormat) ? "Delimited" : FrameFormat.Trim();
        entity.FrameStartFilter = string.IsNullOrWhiteSpace(FrameStartFilter) ? null : FrameStartFilter;
        entity.IgnorePrefix = string.IsNullOrWhiteSpace(IgnorePrefix) ? null : IgnorePrefix;
        entity.Delimiter = string.IsNullOrEmpty(Delimiter) ? "," : Delimiter;
        entity.RegexPattern = string.IsNullOrWhiteSpace(RegexPattern) ? null : RegexPattern;
        entity.ValueField = ValueField.Trim();
        entity.ValueMultiplier = ValueMultiplier <= 0 ? 1.0 : ValueMultiplier;
        entity.CounterField = string.IsNullOrWhiteSpace(CounterField) ? null : CounterField.Trim();
        entity.ToolSerialField = string.IsNullOrWhiteSpace(ToolSerialField) ? null : ToolSerialField.Trim();
        entity.UnitField = string.IsNullOrWhiteSpace(UnitField) ? null : UnitField.Trim();
        entity.AngleField = string.IsNullOrWhiteSpace(AngleField) ? null : AngleField.Trim();
        entity.JudgmentField = string.IsNullOrWhiteSpace(JudgmentField) ? null : JudgmentField.Trim();
        entity.PassValue = string.IsNullOrWhiteSpace(PassValue) ? null : PassValue.Trim();
        entity.DoubleTighteningValue = string.IsNullOrWhiteSpace(DoubleTighteningValue) ? null : DoubleTighteningValue.Trim();
        entity.SyncLimitsCommandTemplate = string.IsNullOrWhiteSpace(SyncLimitsCommandTemplate) ? null : SyncLimitsCommandTemplate;
        entity.IsActive = true;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"BLE Protocol '{entity.Name}' saved.";
        return RedirectToAction(nameof(Index), new { tab = "protocols" });
    }

    [HttpPost("DeleteProtocol/{id:int}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProtocol(int id)
    {
        var proto = await _db.BleProtocols.FindAsync(id);
        if (proto == null)
        {
            TempData["Error"] = "Protocol not found.";
            return RedirectToAction(nameof(Index), new { tab = "protocols" });
        }
        var user = (_userManager != null && User != null) ? await _userManager.GetUserAsync(User) : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);
        if (!isMainAdmin && user?.DepartmentId != null && proto.DepartmentId != user.DepartmentId)
        {
            TempData["Error"] = "You cannot delete a protocol owned by another department or system baseline.";
            return RedirectToAction(nameof(Index), new { tab = "protocols" });
        }
        if (proto.IsBuiltIn)
        {
            TempData["Error"] = $"Built-in protocol '{proto.Name}' cannot be deleted.";
            return RedirectToAction(nameof(Index), new { tab = "protocols" });
        }

        _db.BleProtocols.Remove(proto);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Protocol '{proto.Name}' deleted.";
        return RedirectToAction(nameof(Index), new { tab = "protocols" });
    }

    [HttpGet("ProtocolsJson")]
    [AllowAnonymous]
    public async Task<IActionResult> ProtocolsJson()
    {
        var user = _userManager != null && User.Identity?.IsAuthenticated == true
            ? await _userManager.GetUserAsync(User)
            : null;
        bool isMainAdmin = user != null && _apiKeyService != null && await _apiKeyService.IsMainAdminAsync(user.Id);
        int? deptId = (!isMainAdmin && user?.DepartmentId != null) ? user.DepartmentId : null;

        var protoQuery = _db.BleProtocols.AsNoTracking().Where(p => p.IsActive);
        if (deptId.HasValue)
        {
            protoQuery = protoQuery.Where(p => p.DepartmentId == deptId.Value);
        }

        var protocols = await protoQuery
            .OrderByDescending(p => p.DepartmentId != null)
            .ThenByDescending(p => p.IsBuiltIn)
            .ThenBy(p => p.Name)
            .Select(p => new
            {
                p.BleProtocolId,
                p.Name,
                SupportedInstrumentType = p.SupportedInstrumentType.HasValue ? (int?)p.SupportedInstrumentType.Value : null,
                p.ServiceUuid,
                p.TxCharUuid,
                p.RxCharUuid,
                p.DeviceNamePrefixes,
                p.FrameFormat,
                p.FrameStartFilter,
                p.IgnorePrefix,
                p.Delimiter,
                p.RegexPattern,
                p.ValueField,
                p.ValueMultiplier,
                p.CounterField,
                p.ToolSerialField,
                p.UnitField,
                p.AngleField,
                p.JudgmentField,
                p.PassValue,
                p.DoubleTighteningValue,
                p.SyncLimitsCommandTemplate,
                p.IsBuiltIn
            })
            .ToListAsync();

        var devQuery = _db.Devices
            .AsNoTracking()
            .Where(d => d.IsBluetoothDevice && d.IsActive && d.BleProtocolId != null);
        if (deptId.HasValue)
        {
            devQuery = devQuery.Where(d => d.DepartmentId == deptId.Value);
        }
        var deviceMappings = await devQuery
            .Select(d => new
            {
                d.DeviceId,
                d.Name,
                d.TransmitterId,
                InstrumentType = (int)d.InstrumentType,
                d.BleUuid,
                d.BleProtocolId
            })
            .ToListAsync();

        return Json(new { protocols, deviceMappings });
    }
}
