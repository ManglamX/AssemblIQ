using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Web.Hubs;

namespace SmartTorque.Web.Infrastructure.BackgroundServices;

/// <summary>
/// Dead-Man Fleet Watchdog (IEC 62443 / Industrial Fault Tolerance).
/// Polls active in-progress assembly sessions every 30 seconds.
/// If an active paired tool has not transmitted a heartbeat within 30 seconds,
/// alerts the workstation tablet in real time via SignalR so operators never tighten with a disconnected tool.
/// </summary>
public class DeviceHeartbeatWatchdogService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly IHubContext<LiveReadingHub> _hubContext;
    private readonly ILogger<DeviceHeartbeatWatchdogService> _logger;
    private readonly HashSet<long> _activeAlerts = new();

    public DeviceHeartbeatWatchdogService(
        IServiceProvider services,
        IHubContext<LiveReadingHub> hubContext,
        ILogger<DeviceHeartbeatWatchdogService> logger)
    {
        _services = services;
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Dead-Man Device Heartbeat Watchdog started (30s interval).");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
                await CheckActiveSessionsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error occurred during device watchdog heartbeat cycle.");
            }
        }
    }

    private async Task CheckActiveSessionsAsync(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var cutoff = DateTime.UtcNow.AddSeconds(-30);

        // Fetch all in-progress sessions with a paired torque or vernier device
        var activeSessions = await db.Sessions
            .AsNoTracking()
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Where(s => s.Status == SessionStatus.InProgress && (s.TorqueDeviceId != null || s.VernierDeviceId != null))
            .ToListAsync(ct);

        foreach (var session in activeSessions)
        {
            var primaryDevice = session.TorqueDevice ?? session.VernierDevice;
            if (primaryDevice == null) continue;

            bool isOffline = !primaryDevice.LastSeenAt.HasValue || primaryDevice.LastSeenAt.Value < cutoff;

            if (isOffline)
            {
                if (_activeAlerts.Add(session.SessionId))
                {
                    _logger.LogWarning("Watchdog Alert: Tool {TransmitterId} ({DeviceName}) in Session #{SessionId} lost signal (Last seen: {LastSeen}).",
                        primaryDevice.TransmitterId, primaryDevice.Name, session.SessionId, primaryDevice.LastSeenAt);

                    await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("toolSignalLost", new
                    {
                        sessionId = session.SessionId,
                        deviceId = primaryDevice.DeviceId,
                        transmitterId = primaryDevice.TransmitterId,
                        deviceName = primaryDevice.Name,
                        lastSeenAt = primaryDevice.LastSeenAt,
                        message = "Tool Signal Lost — Check Battery / Proximity"
                    }, ct);
                }
            }
            else
            {
                if (_activeAlerts.Remove(session.SessionId))
                {
                    _logger.LogInformation("Watchdog Recovery: Tool {TransmitterId} in Session #{SessionId} reconnected.",
                        primaryDevice.TransmitterId, session.SessionId);

                    await _hubContext.Clients.Group($"session-{session.SessionId}").SendAsync("toolSignalRestored", new
                    {
                        sessionId = session.SessionId,
                        deviceId = primaryDevice.DeviceId,
                        transmitterId = primaryDevice.TransmitterId,
                        deviceName = primaryDevice.Name,
                        lastSeenAt = primaryDevice.LastSeenAt,
                        message = "Tool Signal Restored"
                    }, ct);
                }
            }
        }
    }
}
