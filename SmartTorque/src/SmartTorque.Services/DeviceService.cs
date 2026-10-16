using System.Security.Cryptography;
using System.Text;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

public record RegisterDeviceRequest(
    string TransmitterId,
    string Name,
    InstrumentType InstrumentType,
    string? Location = null,
    string? FirmwareVersion = null,
    DateOnly? CalibrationDue = null,
    string? ExplicitApiKey = null
);

public record RegisterDeviceResult(
    Device Device,
    string RawApiKey
);

public enum DeviceAuthStatus
{
    Success,
    InvalidKey,
    DeviceNotFound,
    DeviceInactive
}

public record DeviceAuthResult(
    DeviceAuthStatus Status,
    Device? Device
);

public record DeviceSessionStepDto(
    long SessionId,
    int JobId,
    string JobNo,
    string ComponentName,
    int StepSequence,
    string StepLabel,
    InstrumentType Instrument,
    decimal Target,
    string Unit,
    decimal LowerLimit,
    decimal UpperLimit,
    int MaxRetries,
    int CurrentAttempt
);

public interface IDeviceService
{
    Task<List<Device>> ListAsync(InstrumentType? instrumentType = null, bool? activeOnly = null, CancellationToken ct = default);
    Task<Device?> GetByIdAsync(int deviceId, CancellationToken ct = default);
    Task<Device?> GetByTransmitterIdAsync(string transmitterId, CancellationToken ct = default);
    Task<RegisterDeviceResult> RegisterAsync(RegisterDeviceRequest request, string createdBy, CancellationToken ct = default);
    Task<Device?> VerifyApiKeyAsync(string transmitterId, string rawApiKey, CancellationToken ct = default);
    Task<DeviceAuthResult> AuthenticateAsync(string transmitterId, string rawApiKey, CancellationToken ct = default);
    Task<Device> UpdateAsync(int deviceId, string name, string? location, DateOnly? calibrationDue, CancellationToken ct = default);
    Task<RegisterDeviceResult> ReissueApiKeyAsync(int deviceId, CancellationToken ct = default);
    Task<DeviceSessionStepDto?> GetActiveSessionStepForDeviceAsync(string transmitterId, CancellationToken ct = default);
    Task UpdateHeartbeatAsync(string transmitterId, string? firmwareVersion = null, CancellationToken ct = default);
    Task SetActiveAsync(int deviceId, bool isActive, CancellationToken ct = default);
}

public class DeviceService : IDeviceService
{
    private readonly AppDbContext _db;
    private readonly IMeasurementEvaluator _evaluator;

    public DeviceService(AppDbContext db, IMeasurementEvaluator? evaluator = null)
    {
        _db = db;
        _evaluator = evaluator ?? new ToleranceService();
    }

    public static string HashApiKey(string rawApiKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawApiKey);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawApiKey));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public async Task<List<Device>> ListAsync(InstrumentType? instrumentType = null, bool? activeOnly = null, CancellationToken ct = default)
    {
        var query = _db.Devices.AsNoTracking().AsQueryable();

        if (instrumentType.HasValue)
        {
            query = query.Where(d => d.InstrumentType == instrumentType.Value);
        }

        if (activeOnly.HasValue)
        {
            query = query.Where(d => d.IsActive == activeOnly.Value);
        }

        return await query.OrderBy(d => d.Name).ToListAsync(ct);
    }

    public async Task<Device?> GetByIdAsync(int deviceId, CancellationToken ct = default)
    {
        return await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
    }

    public async Task<Device?> GetByTransmitterIdAsync(string transmitterId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(transmitterId);
        var normalized = transmitterId.Trim();
        return await _db.Devices.FirstOrDefaultAsync(d => d.TransmitterId == normalized, ct);
    }

    public async Task<RegisterDeviceResult> RegisterAsync(RegisterDeviceRequest request, string createdBy, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (string.IsNullOrWhiteSpace(request.TransmitterId))
        {
            throw new ArgumentException("TransmitterId is required.", nameof(request));
        }
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Device name is required.", nameof(request));
        }

        var normalizedId = request.TransmitterId.Trim();
        var exists = await _db.Devices.AnyAsync(d => d.TransmitterId == normalizedId, ct);
        if (exists)
        {
            throw new InvalidOperationException($"A device with TransmitterId '{normalizedId}' already exists.");
        }

        string rawApiKey = !string.IsNullOrWhiteSpace(request.ExplicitApiKey)
            ? request.ExplicitApiKey.Trim()
            : "st_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        var device = new Device
        {
            TransmitterId = normalizedId,
            Name = request.Name.Trim(),
            InstrumentType = request.InstrumentType,
            Location = request.Location?.Trim(),
            FirmwareVersion = request.FirmwareVersion?.Trim(),
            CalibrationDue = request.CalibrationDue,
            ApiKeyHash = HashApiKey(rawApiKey),
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Devices.Add(device);
        await _db.SaveChangesAsync(ct);

        return new RegisterDeviceResult(device, rawApiKey);
    }

    public async Task<DeviceAuthResult> AuthenticateAsync(string transmitterId, string rawApiKey, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transmitterId) || string.IsNullOrWhiteSpace(rawApiKey))
        {
            return new DeviceAuthResult(DeviceAuthStatus.InvalidKey, null);
        }

        var normalizedId = transmitterId.Trim();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.TransmitterId == normalizedId, ct);
        if (device == null)
        {
            return new DeviceAuthResult(DeviceAuthStatus.DeviceNotFound, null);
        }

        if (!device.IsActive)
        {
            return new DeviceAuthResult(DeviceAuthStatus.DeviceInactive, device);
        }

        var computedHash = HashApiKey(rawApiKey.Trim());
        var storedHashBytes = Encoding.UTF8.GetBytes(device.ApiKeyHash);
        var computedHashBytes = Encoding.UTF8.GetBytes(computedHash);

        if (!CryptographicOperations.FixedTimeEquals(storedHashBytes, computedHashBytes))
        {
            return new DeviceAuthResult(DeviceAuthStatus.InvalidKey, device);
        }

        device.LastSeenAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new DeviceAuthResult(DeviceAuthStatus.Success, device);
    }

    public async Task<Device?> VerifyApiKeyAsync(string transmitterId, string rawApiKey, CancellationToken ct = default)
    {
        var auth = await AuthenticateAsync(transmitterId, rawApiKey, ct);
        return auth.Status == DeviceAuthStatus.Success ? auth.Device : null;
    }

    public async Task<Device> UpdateAsync(int deviceId, string name, string? location, DateOnly? calibrationDue, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found.");

        device.Name = name.Trim();
        device.Location = location?.Trim();
        device.CalibrationDue = calibrationDue;
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return device;
    }

    public async Task<RegisterDeviceResult> ReissueApiKeyAsync(int deviceId, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct)
            ?? throw new KeyNotFoundException($"Device {deviceId} not found.");

        string rawApiKey = "st_" + Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        device.ApiKeyHash = HashApiKey(rawApiKey);
        device.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return new RegisterDeviceResult(device, rawApiKey);
    }

    public async Task<DeviceSessionStepDto?> GetActiveSessionStepForDeviceAsync(string transmitterId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transmitterId)) return null;

        var normalizedId = transmitterId.Trim();
        var device = await _db.Devices.AsNoTracking().FirstOrDefaultAsync(d => d.TransmitterId == normalizedId, ct);
        if (device == null || !device.IsActive) return null;

        var session = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
            .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress &&
                                     (s.TorqueDeviceId == device.DeviceId || s.VernierDeviceId == device.DeviceId), ct);

        if (session == null)
        {
            // Fallback: active session expecting this instrument type
            session = await _db.Sessions
                .AsNoTracking()
                .Include(s => s.Job)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress, ct);
        }

        if (session == null) return null;

        var currentStep = session.ProductComponent.Template.Steps
            .FirstOrDefault(st => st.SequenceNo == session.CurrentStepSequence)
            ?? session.ProductComponent.Template.Steps.FirstOrDefault();

        if (currentStep == null) return null;

        var (lower, upper) = _evaluator.ComputeBand(currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);
        var currentAttempts = await _db.SessionResults.CountAsync(r => r.SessionId == session.SessionId && r.StepId == currentStep.TemplateStepId, ct);

        return new DeviceSessionStepDto(
            SessionId: session.SessionId,
            JobId: session.JobId,
            JobNo: session.Job.JobNo,
            ComponentName: session.ProductComponent.ComponentName,
            StepSequence: currentStep.SequenceNo,
            StepLabel: currentStep.Label,
            Instrument: currentStep.Instrument,
            Target: currentStep.Target,
            Unit: currentStep.Instrument == InstrumentType.Torque ? "N·m" : "mm",
            LowerLimit: lower,
            UpperLimit: upper,
            MaxRetries: currentStep.MaxRetries,
            CurrentAttempt: currentAttempts + 1
        );
    }

    public async Task UpdateHeartbeatAsync(string transmitterId, string? firmwareVersion = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(transmitterId)) return;

        var normalizedId = transmitterId.Trim();
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.TransmitterId == normalizedId, ct);
        if (device == null) return;

        device.LastSeenAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(firmwareVersion))
        {
            device.FirmwareVersion = firmwareVersion.Trim();
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task SetActiveAsync(int deviceId, bool isActive, CancellationToken ct = default)
    {
        var device = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == deviceId, ct);
        if (device == null) return;

        device.IsActive = isActive;
        device.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }
}