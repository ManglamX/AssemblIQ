using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;

namespace SmartTorque.Services;

/// <summary>
/// Builds worker-facing notices. Pure — it owns no state and touches no database, so
/// the caller decides when the row is written (see <see cref="INotificationService"/>).
/// </summary>
public sealed class NotificationService : INotificationService
{
    /// <summary>How many JobNos are spelled out before the notice summarises. This is
    /// what keeps <c>Notification.Message</c> inside its 500-character column.</summary>
    private const int MaxJobNumbersListed = 5;

    public Notification CreateBlackJobNotice(IReadOnlyList<Job> blackJobs)
    {
        if (blackJobs.Count == 0)
            throw new InvalidOperationException("A Black-job notice needs at least one Black job.");

        var jobNos = string.Join(", ", blackJobs.Take(MaxJobNumbersListed).Select(j => j.JobNo));
        if (blackJobs.Count > MaxJobNumbersListed)
            jobNos += $" and {blackJobs.Count - MaxJobNumbersListed} more";

        var message = blackJobs.Count == 1
            ? $"Urgent Black job {jobNos} needs a worker."
            : $"{blackJobs.Count} urgent Black jobs need a worker: {jobNos}.";

        return new Notification
        {
            Type = NotificationType.BlackJob,
            // Listing at most five JobNos is what keeps this inside the 500-char column;
            // the large-batch test guards that invariant.
            Message = message,
            TargetRole = "Worker",
            CreatedAt = DateTime.UtcNow,
            // Points at the first Black job so the board can link straight to it.
            // Set through the navigation, not JobId — the ids are not generated yet.
            Job = blackJobs[0]
        };
    }
}