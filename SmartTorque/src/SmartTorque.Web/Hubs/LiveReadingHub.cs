using Microsoft.AspNetCore.SignalR;

namespace SmartTorque.Web.Hubs;

/// <summary>
/// SignalR hub broadcasting real-time readings and step progression
/// to the worker execution cockpit (Architecture §5.8).
/// </summary>
public class LiveReadingHub : Hub
{
    public async Task JoinJob(int jobId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }

    public async Task LeaveJob(int jobId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"job-{jobId}");
    }

    public async Task JoinSession(long sessionId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }

    public async Task LeaveSession(long sessionId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"session-{sessionId}");
    }

    public async Task JoinWorkers()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "workers");
    }

    public async Task LeaveWorkers()
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, "workers");
    }
}