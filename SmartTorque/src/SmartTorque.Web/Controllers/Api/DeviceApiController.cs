using System.ComponentModel.DataAnnotations;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Hubs;

namespace SmartTorque.Web.Controllers.Api;

public class DeviceRegisterDto
{
    [Required]
    public string TransmitterId { get; set; } = string.Empty;
    public string? Firmware { get; set; }
    public InstrumentType? Instrument { get; set; }
}

public class DeviceHeartbeatDto
{
    [Required]
    public string TransmitterId { get; set; } = string.Empty;
    public string? FirmwareVersion { get; set; }
    public int? BatteryLevel { get; set; }
    public int? SignalStrength { get; set; }
}

public class DeviceReadingDto
{
    public string? TransmitterId { get; set; }
    public InstrumentType? Instrument { get; set; }
    public decimal Value { get; set; }
    public string? Unit { get; set; }
    public bool IsStable { get; set; } = true;
    public long? Seq { get; set; }
    public long? DeviceSeq { get => Seq; set => Seq = value; }
    public DateTime? Timestamp { get; set; }
    public long? SessionId { get; set; }
}

public class BatchReadingSyncDto
{
    public string? TransmitterId { get; set; }
    public List<DeviceReadingDto> Readings { get; set; } = new();
}

public class ActivityWindowSyncDto
{
    public long SessionId { get; set; }
    public string? WorkerId { get; set; }
    public int? DeviceId { get; set; }
    public DateTime WindowStart { get; set; }
    public DateTime? WindowEnd { get; set; }
}

[ApiController]
[Route("api/device")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
[EnableRateLimiting("device-rate-limit")]
public class DeviceApiController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ISessionService _sessionService;
    private readonly IMeasurementEvaluator _evaluator;
    private readonly IHubContext<LiveReadingHub> _hubContext;
    private readonly AppDbContext _db;
    private readonly ILogger<DeviceApiController> _logger;

    public DeviceApiController(
        IDeviceService deviceService,
        ISessionService sessionService,
        IMeasurementEvaluator evaluator,
        IHubContext<LiveReadingHub> hubContext,
        AppDbContext db,
        ILogger<DeviceApiController> logger)
    {
        _deviceService = deviceService;
        _sessionService = sessionService;
        _evaluator = evaluator;
        _hubContext = hubContext;
        _db = db;
        _logger = logger;
    }

    private async Task LogSecurityIncidentAsync(string action, string reason, string? transmitterId)
    {
        try
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = Request.Headers.UserAgent.ToString();
            _db.AuditLogs.Add(new SmartTorque.Core.Entities.AuditLog
            {
                OccurredAt = DateTime.UtcNow,
                EntityName = "DeviceSecurityGateway",
                EntityId = transmitterId ?? ip,
                Action = action,
                Changes = System.Text.Json.JsonSerializer.Serialize(new { ip, userAgent, reason, transmitterId })
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log security incident");
        }
    }

    private async Task<(Device? Device, IActionResult? ErrorResult)> AuthenticateDeviceAsync(string? transmitterIdFromRequest, CancellationToken ct)
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var headerTransmitter = Request.Headers["X-Transmitter-Id"].FirstOrDefault();
        var transmitterId = !string.IsNullOrWhiteSpace(headerTransmitter)
            ? headerTransmitter.Trim()
            : transmitterIdFromRequest?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            await LogSecurityIncidentAsync("AUTH_FAILED_MISSING_API_KEY", "Request missing X-Api-Key header", transmitterId);
            return (null, Unauthorized(new ProblemDetails
            {
                Title = "Missing API Key",
                Detail = "Device request must include the 'X-Api-Key' header.",
                Status = StatusCodes.Status401Unauthorized
            }));
        }

        if (string.IsNullOrWhiteSpace(transmitterId))
        {
            await LogSecurityIncidentAsync("AUTH_FAILED_MISSING_TRANSMITTER", "Request missing transmitter ID", null);
            return (null, BadRequest(new ProblemDetails
            {
                Title = "Missing Transmitter ID",
                Detail = "Device request must specify TransmitterId in header or payload.",
                Status = StatusCodes.Status400BadRequest
            }));
        }

        var authResult = await _deviceService.AuthenticateAsync(transmitterId, apiKey, ct);
        if (authResult.Status != DeviceAuthStatus.Success)
        {
            await LogSecurityIncidentAsync($"AUTH_FAILED_{authResult.Status}", $"Device authentication rejected: {authResult.Status}", transmitterId);
        }

        return authResult.Status switch
        {
            DeviceAuthStatus.Success => (authResult.Device, null),
            DeviceAuthStatus.DeviceInactive => (null, StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Title = "Device Inactive",
                Detail = $"Device '{transmitterId}' is inactive. Contact the system administrator.",
                Status = StatusCodes.Status403Forbidden
            })),
            DeviceAuthStatus.DeviceNotFound => (null, Unauthorized(new ProblemDetails
            {
                Title = "Device Not Found",
                Detail = $"No registered device found with TransmitterId '{transmitterId}'.",
                Status = StatusCodes.Status401Unauthorized
            })),
            _ => (null, Unauthorized(new ProblemDetails
            {
                Title = "Invalid API Key",
                Detail = "The provided API key is invalid for this transmitter.",
                Status = StatusCodes.Status401Unauthorized
            }))
        };
    }

    /// <summary>
    /// POST /api/device/register
    /// Device announces itself, updates firmware and heartbeat.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] DeviceRegisterDto dto, CancellationToken ct)
    {
        var (device, errorResult) = await AuthenticateDeviceAsync(dto.TransmitterId, ct);
        if (errorResult != null) return errorResult;

        if (dto.Instrument.HasValue && dto.Instrument.Value != device!.InstrumentType)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Instrument Type Mismatch",
                Detail = $"Transmitter '{device.TransmitterId}' is registered as {device.InstrumentType}, but registration announced {dto.Instrument.Value}.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        await _deviceService.UpdateHeartbeatAsync(device!.TransmitterId, dto.Firmware, ct);
        await _hubContext.Clients.Group("admins").SendAsync("deviceStatus", new
        {
            transmitterId = device.TransmitterId,
            online = true,
            firmwareVersion = dto.Firmware ?? device.FirmwareVersion,
            lastSeenAt = DateTime.UtcNow
        }, ct);

        return Ok(new
        {
            registered = true,
            transmitterId = device.TransmitterId,
            name = device.Name,
            instrument = device.InstrumentType.ToString(),
            firmware = dto.Firmware ?? device.FirmwareVersion
        });
    }

    /// <summary>
    /// POST /api/device/heartbeat
    /// Keep-alive ping sent by ESP32 gateway every ~10s.
    /// </summary>
    [HttpPost("heartbeat")]
    public async Task<IActionResult> Heartbeat([FromBody] DeviceHeartbeatDto dto, CancellationToken ct)
    {
        var (device, errorResult) = await AuthenticateDeviceAsync(dto.TransmitterId, ct);
        if (errorResult != null) return errorResult;

        await _deviceService.UpdateHeartbeatAsync(device!.TransmitterId, dto.FirmwareVersion, ct);

        await _hubContext.Clients.Group("admins").SendAsync("deviceStatus", new
        {
            transmitterId = device.TransmitterId,
            online = true,
            batteryLevel = dto.BatteryLevel,
            signalStrength = dto.SignalStrength,
            lastSeenAt = DateTime.UtcNow
        }, ct);

        return Ok(new
        {
            status = "ok",
            transmitterId = device.TransmitterId,
            serverTime = DateTime.UtcNow
        });
    }

    /// <summary>
    /// POST /api/device/reading
    /// Ingests live samples and confirmed measurements from ESP32 gateways.
    /// </summary>
    [HttpPost("reading")]
    public async Task<IActionResult> PostReading([FromBody] DeviceReadingDto dto, CancellationToken ct)
    {
        var (device, errorResult) = await AuthenticateDeviceAsync(dto.TransmitterId, ct);
        if (errorResult != null) return errorResult;

        // Broadcast device status update to Admins dashboard via SignalR
        await _hubContext.Clients.Group("admins").SendAsync("deviceStatus", new
        {
            transmitterId = device!.TransmitterId,
            online = true,
            lastSeenAt = DateTime.UtcNow
        }, ct);

        // 1. Validate unit for instrument type
        if (!string.IsNullOrWhiteSpace(dto.Unit))
        {
            var u = dto.Unit.Trim().ToLowerInvariant();
            if (device!.InstrumentType == InstrumentType.Torque && u != "nm" && u != "n·m" && u != "n.m")
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Measurement Unit",
                    Detail = $"Torque wrench device requires unit 'Nm' or 'N·m', received '{dto.Unit}'.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
            if (device.InstrumentType == InstrumentType.Vernier && u != "mm")
            {
                return BadRequest(new ProblemDetails
                {
                    Title = "Invalid Measurement Unit",
                    Detail = $"Vernier caliper device requires unit 'mm', received '{dto.Unit}'.",
                    Status = StatusCodes.Status400BadRequest
                });
            }
        }

        // 2. Validate timestamp sanity (reject future timestamps > 5 minutes)
        if (dto.Timestamp.HasValue && dto.Timestamp.Value.ToUniversalTime() > DateTime.UtcNow.AddMinutes(5))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Timestamp",
                Detail = "Measurement timestamp cannot be in the future (> 5 minutes).",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // 3. Hardware Replay Protection (Monotonic Sequence Counter - IEC 62443)
        if (dto.DeviceSeq.HasValue && dto.DeviceSeq.Value > 0)
        {
            if (device!.LastSeenSeq > 0 && dto.DeviceSeq.Value < device.LastSeenSeq)
            {
                var isDuplicate = await _db.SessionResults
                    .AnyAsync(r => r.DeviceId == device.DeviceId && r.DeviceSeq == dto.DeviceSeq.Value && r.ActualValue == dto.Value, ct);

                if (!isDuplicate)
                {
                    await LogSecurityIncidentAsync("HARDWARE_REPLAY_QUARANTINE", $"Received out-of-order sequence #{dto.DeviceSeq.Value} with differing value (Last seen max seq: #{device.LastSeenSeq})", device.TransmitterId);
                    return StatusCode(StatusCodes.Status400BadRequest, new ProblemDetails
                    {
                        Title = "Hardware Replay Violation",
                        Detail = $"Received out-of-order sequence #{dto.DeviceSeq.Value} (Last verified sequence: #{device.LastSeenSeq}). Packet quarantined as tampering attempt.",
                        Status = StatusCodes.Status400BadRequest
                    });
                }
            }
            else if (dto.DeviceSeq.Value > device!.LastSeenSeq)
            {
                device.LastSeenSeq = dto.DeviceSeq.Value;
            }
        }

        // 4. Find active session for this device or requested session
        Session? session = null;
        if (dto.SessionId.HasValue)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
                    .ThenInclude(j => j.Product)
                        .ThenInclude(p => p.Components)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.SessionId == dto.SessionId.Value, ct);
        }

        // 4a. Prioritize timeline window matching if reading has a timestamp (buffered / offline readings)
        if (session == null && dto.Timestamp.HasValue)
        {
            var readingTime = dto.Timestamp.Value.ToUniversalTime();
            // Allow 30s tolerance on activity window edges to account for NTP clock drift and Wi-Fi latency
            var window = await _db.SessionActivityWindows
                .Include(w => w.Session)
                    .ThenInclude(s => s.Job)
                        .ThenInclude(j => j.Product)
                            .ThenInclude(p => p.Components)
                .Include(w => w.Session)
                    .ThenInclude(s => s.ProductComponent)
                        .ThenInclude(c => c.Template)
                            .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .Where(w => w.Session.Job.Status == JobStatus.InProgress &&
                            (w.DeviceId == null || w.DeviceId == device!.DeviceId) &&
                            w.WindowStart.AddSeconds(-30) <= readingTime &&
                            (w.WindowEnd == null || w.WindowEnd.Value.AddSeconds(30) >= readingTime))
                .OrderByDescending(w => w.WindowStart)
                .FirstOrDefaultAsync(ct);

            if (window?.Session != null)
            {
                if (window.Session.Status == SessionStatus.Completed)
                {
                    // If this window's session already completed, check if another component session on this unit is in progress
                    var nextActive = await _db.Sessions
                        .Include(s => s.Job)
                            .ThenInclude(j => j.Product)
                                .ThenInclude(p => p.Components)
                        .Include(s => s.ProductComponent)
                            .ThenInclude(c => c.Template)
                                .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                        .FirstOrDefaultAsync(s => s.JobId == window.Session.JobId &&
                                                  s.UnitNo == window.Session.UnitNo &&
                                                  s.Status == SessionStatus.InProgress, ct);

                    // Or look for an in-progress session on the next unit
                    if (nextActive == null)
                    {
                        nextActive = await _db.Sessions
                            .Include(s => s.Job)
                                .ThenInclude(j => j.Product)
                                    .ThenInclude(p => p.Components)
                            .Include(s => s.ProductComponent)
                                .ThenInclude(c => c.Template)
                                    .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                            .OrderBy(s => s.UnitNo)
                            .FirstOrDefaultAsync(s => s.JobId == window.Session.JobId &&
                                                      s.UnitNo > window.Session.UnitNo &&
                                                      s.Status == SessionStatus.InProgress, ct);
                    }

                    session = nextActive; // Only use if an in-progress session actually exists!
                }
                else
                {
                    session = window.Session;
                }
            }
        }

        // 4b. Find session with currently open activity window for this device or worker station
        if (session == null)
        {
            var openWindow = await _db.SessionActivityWindows
                .Include(w => w.Session)
                    .ThenInclude(s => s.Job)
                        .ThenInclude(j => j.Product)
                            .ThenInclude(p => p.Components)
                .Include(w => w.Session)
                    .ThenInclude(s => s.ProductComponent)
                        .ThenInclude(c => c.Template)
                            .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .Where(w => w.WindowEnd == null &&
                            w.Session.Status == SessionStatus.InProgress &&
                            w.Session.Job.Status == JobStatus.InProgress &&
                            (w.DeviceId == null || w.DeviceId == device!.DeviceId ||
                             w.Session.TorqueDeviceId == device.DeviceId || w.Session.VernierDeviceId == device.DeviceId))
                .OrderByDescending(w => w.WindowStart)
                .FirstOrDefaultAsync(ct);

            session = openWindow?.Session;
        }

        // 4c. Fallback to currently bound session for real-time live readings (within active jobs only)
        if (session == null)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
                    .ThenInclude(j => j.Product)
                        .ThenInclude(p => p.Components)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .OrderByDescending(s => s.StartTime)
                .ThenByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress &&
                                          s.Job.Status == JobStatus.InProgress &&
                                         (s.TorqueDeviceId == device!.DeviceId || s.VernierDeviceId == device.DeviceId), ct);
        }

        if (session == null)
        {
            // Fallback: active session expecting this instrument type on active jobs only
            session = await _db.Sessions
                .Include(s => s.Job)
                    .ThenInclude(j => j.Product)
                        .ThenInclude(p => p.Components)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .OrderByDescending(s => s.StartTime)
                .ThenByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress &&
                                          s.Job.Status == JobStatus.InProgress, ct);

            // Automatically bind device to this session so future readings are direct
            if (session != null)
            {
                if (device!.InstrumentType == InstrumentType.Torque)
                {
                    session.TorqueDeviceId = device.DeviceId;
                }
                else if (device!.InstrumentType == InstrumentType.Vernier)
                {
                    session.VernierDeviceId = device.DeviceId;
                }
                await _db.SaveChangesAsync(ct);
            }
        }

        if (session == null)
        {
            return StatusCode(StatusCodes.Status202Accepted, new
            {
                accepted = false,
                reason = "NO_ACTIVE_SESSION",
                transmitterId = device!.TransmitterId
            });
        }

        var steps = session.ProductComponent.Template.Steps.OrderBy(s => s.SequenceNo).ToList();
        var currentStep = steps.FirstOrDefault(s => s.SequenceNo == session.CurrentStepSequence)
            ?? steps.FirstOrDefault();

        if (currentStep == null)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Missing Template Steps",
                Detail = "The component template has no steps defined.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var (lower, upper) = _evaluator.ComputeBand(currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);
        bool inTolerance = _evaluator.IsInBand(dto.Value, currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);

        // 4. Instrument mismatch check
        bool isDevOrUniversal = device!.TransmitterId.StartsWith("esp", StringComparison.OrdinalIgnoreCase) ||
                                device.TransmitterId.StartsWith("SIM", StringComparison.OrdinalIgnoreCase);
        if (currentStep.Instrument != device.InstrumentType && !isDevOrUniversal)
        {
            await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("hint", new
            {
                code = "UNEXPECTED_INSTRUMENT",
                message = $"Step {currentStep.SequenceNo} expects {currentStep.Instrument}, but reading came from {device.InstrumentType} ({device.TransmitterId})."
            }, ct);

            return StatusCode(StatusCodes.Status409Conflict, new
            {
                accepted = false,
                reason = "UNEXPECTED_INSTRUMENT",
                expected = currentStep.Instrument.ToString(),
                received = device.InstrumentType.ToString()
            });
        }

        // 5. Live sample stream (!IsStable)
        if (!dto.IsStable)
        {
            await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("liveValue", new
            {
                sessionId = session.SessionId,
                stepId = currentStep.TemplateStepId,
                instrument = currentStep.Instrument.ToString(),
                value = dto.Value,
                unit = dto.Unit ?? (currentStep.Instrument == InstrumentType.Torque ? "N·m" : "mm"),
                isStable = false,
                inTolerance = inTolerance,
                lower = lower,
                upper = upper,
                ts = dto.Timestamp?.ToUniversalTime() ?? DateTime.UtcNow
            }, ct);

            return Ok(new
            {
                accepted = false,
                live = true,
                inTolerance = inTolerance,
                lower = lower,
                upper = upper
            });
        }

        // 5b. Sliding-Window Debounce (Method 2: 1.5s Transient Window for LIVE streaming only)
        // Catches rapid Wi-Fi packet retries within 1.5 seconds without double-advancing steps.
        // For offline LittleFS batch flushes, every distinct entry was an intentional operator pull and is never dropped.
        var readingUtc = dto.Timestamp?.ToUniversalTime() ?? DateTime.UtcNow;
        var isLiveStream = !dto.Timestamp.HasValue || (DateTime.UtcNow - readingUtc < TimeSpan.FromSeconds(3));

        if (isLiveStream)
        {
            var debounceCutoff = DateTime.UtcNow.AddSeconds(-1.5);
            var recentReading = await _db.SessionResults
                .Include(r => r.Step)
                .Where(r => r.SessionId == session.SessionId &&
                            r.DeviceId == device!.DeviceId &&
                            r.ReceivedAt >= debounceCutoff)
                .OrderByDescending(r => r.ReceivedAt)
                .FirstOrDefaultAsync(ct);

            if (recentReading != null && Math.Abs(recentReading.ActualValue - dto.Value) < 0.05m)
            {
                return Ok(new
                {
                    accepted = true,
                    duplicate = true,
                    result = recentReading.Result.ToString(),
                    lower = recentReading.LowerLimit,
                    upper = recentReading.UpperLimit,
                    step = recentReading.Step.SequenceNo,
                    next = session.CurrentStepSequence,
                    isSessionCompleted = session.Status == SessionStatus.Completed
                });
            }
        }

        // 6. Confirmed stable reading (IsStable == true)
        var command = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: device.DeviceId,
            TransmitterId: device.TransmitterId,
            DeviceSeq: null,
            Value: dto.Value,
            InstrumentType: device.InstrumentType,
            IsManualEntry: false,
            CapturedAt: dto.Timestamp?.ToUniversalTime() ?? DateTime.UtcNow
        );

        var eval = await _sessionService.ProcessReadingAsync(command, ct);
        if (!eval.Success)
        {
            if (eval.ErrorMessage == "UNEXPECTED_INSTRUMENT")
            {
                return StatusCode(StatusCodes.Status409Conflict, new
                {
                    accepted = false,
                    reason = "UNEXPECTED_INSTRUMENT"
                });
            }
            return BadRequest(new { error = eval.ErrorMessage });
        }

        var isCalOverdue = device.CalibrationDue.HasValue && device.CalibrationDue.Value < DateOnly.FromDateTime(DateTime.UtcNow);

        // Broadcast to SignalR (both session and job groups)
        var stepResultData = new
        {
            sessionId = eval.SessionId,
            stepId = eval.StepId,
            attemptNo = eval.AttemptNo,
            actual = eval.ActualValue,
            result = eval.Result.ToString(),
            lower = eval.LowerLimit,
            upper = eval.UpperLimit,
            isStepPassed = eval.IsStepPassed,
            isSessionCompleted = eval.IsSessionCompleted,
            nextStepSequence = eval.NextStepSequence,
            isCalibrationOverdue = isCalOverdue,
            toolCalibrationDue = device.CalibrationDue?.ToString("yyyy-MM-dd")
        };

        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("stepResult", stepResultData, ct);
        await _hubContext.Clients.Group($"job-{session.JobId}").SendAsync("stepResult", stepResultData, ct);

        // Also send ReadingProcessed event for any existing clients
        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("ReadingProcessed", eval, ct);
        await _hubContext.Clients.Group($"job-{session.JobId}").SendAsync("ReadingProcessed", eval, ct);

        if (eval.IsStepPassed)
        {
            if (eval.IsSessionCompleted)
            {
                await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("sessionReady", new
                {
                    sessionId = eval.SessionId
                }, ct);

                await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("SessionCompleted", new
                {
                    sessionId = eval.SessionId,
                    finalResult = "Pass"
                }, ct);

                await _hubContext.Clients.Group($"job-{session.JobId}").SendAsync("SessionCompleted", new
                {
                    sessionId = eval.SessionId,
                    finalResult = "Pass"
                }, ct);
            }
            else
            {
                var nextStep = steps.FirstOrDefault(s => s.SequenceNo == eval.NextStepSequence);
                if (nextStep != null)
                {
                    var (nextLower, nextUpper) = _evaluator.ComputeBand(nextStep.Target, nextStep.ToleranceMinus, nextStep.TolerancePlus, nextStep.ToleranceMode);
                    await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("stepChanged", new
                    {
                        sessionId = eval.SessionId,
                        stepSequence = nextStep.SequenceNo,
                        stepId = nextStep.TemplateStepId,
                        instrument = nextStep.Instrument.ToString(),
                        label = nextStep.Label,
                        target = nextStep.Target,
                        tolerancePlus = nextStep.TolerancePlus,
                        toleranceMinus = nextStep.ToleranceMinus,
                        unit = nextStep.Instrument == InstrumentType.Torque ? "N·m" : "mm",
                        markerX = nextStep.MarkerX,
                        markerY = nextStep.MarkerY,
                        markerSize = nextStep.MarkerSize,
                        totalSteps = steps.Count
                    }, ct);
                }
            }
        }
        else if (eval.IsExceededRetries)
        {
            await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("sessionFailed", new
            {
                sessionId = eval.SessionId,
                reason = "MAX_RETRIES_EXCEEDED"
            }, ct);
        }

        return Ok(new
        {
            accepted = true,
            duplicate = false,
            result = eval.Result.ToString(),
            lower = eval.LowerLimit,
            upper = eval.UpperLimit,
            step = eval.StepSequence,
            next = eval.NextStepSequence,
            isSessionCompleted = eval.IsSessionCompleted,
            calibrationOverdue = isCalOverdue,
            calibrationDue = device.CalibrationDue?.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>
    /// GET /api/device/session/{transmitterId}
    /// Returns the current step that the device is expected to serve, or 204 NoContent if none.
    /// </summary>
    [HttpGet("session/{transmitterId}")]
    public async Task<IActionResult> GetSession([FromRoute] string transmitterId, CancellationToken ct)
    {
        var (device, errorResult) = await AuthenticateDeviceAsync(transmitterId, ct);
        if (errorResult != null) return errorResult;

        var stepInfo = await _deviceService.GetActiveSessionStepForDeviceAsync(transmitterId, ct);
        if (stepInfo == null)
        {
            return NoContent();
        }

        return Ok(stepInfo);
    }

    /// <summary>
    /// POST /api/device/sync-windows
    /// Syncs offline session switch windows recorded by tablet while Wi-Fi was down.
    /// </summary>
    [HttpPost("sync-windows")]
    public async Task<IActionResult> SyncWindows([FromBody] List<ActivityWindowSyncDto> windows, CancellationToken ct)
    {
        if (windows == null || windows.Count == 0) return Ok(new { synced = 0 });

        foreach (var w in windows)
        {
            _db.SessionActivityWindows.Add(new SessionActivityWindow
            {
                SessionId = w.SessionId,
                WorkerId = w.WorkerId,
                DeviceId = w.DeviceId,
                WindowStart = w.WindowStart,
                WindowEnd = w.WindowEnd
            });
        }
        await _db.SaveChangesAsync(ct);
        return Ok(new { synced = windows.Count });
    }

    /// <summary>
    /// POST /api/device/sync-batch
    /// Flushes offline readings buffered in ESP32 internal LittleFS flash after Wi-Fi recovery.
    /// </summary>
    [HttpPost("sync-batch")]
    public async Task<IActionResult> SyncBatch([FromBody] BatchReadingSyncDto batch, CancellationToken ct)
    {
        if (batch?.Readings == null || batch.Readings.Count == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Empty Batch", Detail = "No readings provided in batch payload." });
        }

        var results = new List<object>();
        int processedCount = 0;
        int duplicateCount = 0;

        var sortedReadings = batch.Readings.OrderBy(r => r.Timestamp ?? DateTime.UtcNow).ToList();

        foreach (var reading in sortedReadings)
        {
            if (string.IsNullOrWhiteSpace(reading.TransmitterId) && !string.IsNullOrWhiteSpace(batch.TransmitterId))
            {
                reading.TransmitterId = batch.TransmitterId;
            }

            var actionResult = await PostReading(reading, ct);
            if (actionResult is OkObjectResult ok && ok.Value != null)
            {
                results.Add(ok.Value);
                var dyn = ok.Value;
                var prop = dyn.GetType().GetProperty("duplicate");
                if (prop != null && (bool)prop.GetValue(dyn)!)
                {
                    duplicateCount++;
                }
                else
                {
                    processedCount++;
                }
            }
        }

        return Ok(new
        {
            success = true,
            total = sortedReadings.Count,
            processed = processedCount,
            duplicates = duplicateCount,
            results
        });
    }
}