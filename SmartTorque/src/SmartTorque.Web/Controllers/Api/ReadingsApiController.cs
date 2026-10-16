using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

using SmartTorque.Core.Enums;
using SmartTorque.Services;
using SmartTorque.Web.Hubs;

namespace SmartTorque.Web.Controllers.Api;

public class DeviceReadingRequest
{
    public string? TransmitterId { get; set; }
    public string? ApiKey { get; set; }
    public long? DeviceSeq { get; set; }
    public decimal Value { get; set; }
    public InstrumentType? InstrumentType { get; set; }
    public DateTime? CapturedAt { get; set; }
    public long? SessionId { get; set; }
}

[ApiController]
[Route("api/readings")]
[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ReadingsApiController : ControllerBase
{
    private readonly IDeviceService _deviceService;
    private readonly ISessionService _sessionService;
    private readonly IHubContext<LiveReadingHub> _hubContext;

    public ReadingsApiController(
        IDeviceService deviceService,
        ISessionService sessionService,
        IHubContext<LiveReadingHub> hubContext)
    {
        _deviceService = deviceService;
        _sessionService = sessionService;
        _hubContext = hubContext;
    }

    [HttpPost]
    public async Task<IActionResult> PostReading([FromBody] DeviceReadingRequest request, CancellationToken ct)
    {
        var transmitterId = Request.Headers["X-Transmitter-Id"].FirstOrDefault() ?? request.TransmitterId;
        var apiKey = Request.Headers["X-Api-Key"].FirstOrDefault() ?? request.ApiKey;

        if (string.IsNullOrWhiteSpace(transmitterId) || string.IsNullOrWhiteSpace(apiKey))
        {
            return Unauthorized(new { error = "Missing transmitter credentials (X-Transmitter-Id / X-Api-Key)." });
        }

        var device = await _deviceService.VerifyApiKeyAsync(transmitterId, apiKey, ct);
        if (device == null)
        {
            return Unauthorized(new { error = "Invalid transmitter credentials or device is inactive." });
        }

        var instrumentType = request.InstrumentType ?? device.InstrumentType;

        var command = new ProcessReadingCommand(
            SessionId: request.SessionId,
            DeviceId: device.DeviceId,
            TransmitterId: device.TransmitterId,
            DeviceSeq: request.DeviceSeq,
            Value: request.Value,
            InstrumentType: instrumentType,
            IsManualEntry: false,
            CapturedAt: request.CapturedAt ?? DateTime.UtcNow
        );

        var eval = await _sessionService.ProcessReadingAsync(command, ct);
        if (!eval.Success)
        {
            return BadRequest(new { error = eval.ErrorMessage });
        }

        // Broadcast to SignalR clients on this session & job
        await _hubContext.Clients.Group($"session-{eval.SessionId}").SendAsync("ReadingProcessed", eval, ct);

        return Ok(eval);
    }
}