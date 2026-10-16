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
    public DateTime? Timestamp { get; set; }
    public long? SessionId { get; set; }
}

[ApiController]
[Route("api/device")]
[AllowAnonymous]
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

    private async Task<(Device? Device, IActionResult? ErrorResult)> AuthenticateDeviceAsync(string? transmitterIdFromRequest, CancellationToken ct)
    {
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault();
        var headerTransmitter = Request.Headers["X-Transmitter-Id"].FirstOrDefault();
        var transmitterId = !string.IsNullOrWhiteSpace(headerTransmitter)
            ? headerTransmitter.Trim()
            : transmitterIdFromRequest?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (null, Unauthorized(new ProblemDetails
            {
                Title = "Missing API Key",
                Detail = "Device request must include the 'X-Api-Key' header.",
                Status = StatusCodes.Status401Unauthorized
            }));
        }

        if (string.IsNullOrWhiteSpace(transmitterId))
        {
            return (null, BadRequest(new ProblemDetails
            {
                Title = "Missing Transmitter ID",
                Detail = "Device request must specify TransmitterId in header or payload.",
                Status = StatusCodes.Status400BadRequest
            }));
        }

        var authResult = await _deviceService.AuthenticateAsync(transmitterId, apiKey, ct);
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
        if (dto.Timestamp.HasValue && dto.Timestamp.Value > DateTime.UtcNow.AddMinutes(5))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Invalid Timestamp",
                Detail = "Measurement timestamp cannot be in the future (> 5 minutes).",
                Status = StatusCodes.Status400BadRequest
            });
        }

        // 3. Deduplication check by (DeviceId, Seq) per Architecture §7.5 rule 3
        if (dto.Seq.HasValue)
        {
            var dup = await _db.SessionResults
                .Include(r => r.Step)
                .FirstOrDefaultAsync(r => r.DeviceId == device!.DeviceId && r.DeviceSeq == dto.Seq.Value, ct);

            if (dup != null)
            {
                return Ok(new
                {
                    accepted = true,
                    duplicate = true,
                    result = dup.Result.ToString(),
                    lower = dup.LowerLimit,
                    upper = dup.UpperLimit,
                    step = dup.Step.SequenceNo,
                    next = dup.Step.SequenceNo
                });
            }
        }

        // 4. Find active session for this device or requested session
        Session? session = null;
        if (dto.SessionId.HasValue)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.SessionId == dto.SessionId.Value, ct);
        }

        if (session == null)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress &&
                                         (s.TorqueDeviceId == device!.DeviceId || s.VernierDeviceId == device.DeviceId), ct);
        }

        if (session == null)
        {
            // Fallback: active session expecting this instrument type
            session = await _db.Sessions
                .Include(s => s.Job)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress, ct);
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
        if (currentStep.Instrument != device!.InstrumentType)
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
                ts = dto.Timestamp ?? DateTime.UtcNow
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

        // 6. Confirmed stable reading (IsStable == true)
        var command = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: device.DeviceId,
            TransmitterId: device.TransmitterId,
            DeviceSeq: dto.Seq,
            Value: dto.Value,
            InstrumentType: device.InstrumentType,
            IsManualEntry: false,
            CapturedAt: dto.Timestamp ?? DateTime.UtcNow
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

        // Broadcast to SignalR
        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("stepResult", new
        {
            sessionId = eval.SessionId,
            stepId = eval.StepId,
            attemptNo = eval.AttemptNo,
            actual = eval.ActualValue,
            result = eval.Result.ToString(),
            lower = eval.LowerLimit,
            upper = eval.UpperLimit
        }, ct);

        // Also send legacy ReadingProcessed event for any existing clients
        await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("ReadingProcessed", eval, ct);

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
            isSessionCompleted = eval.IsSessionCompleted
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
}