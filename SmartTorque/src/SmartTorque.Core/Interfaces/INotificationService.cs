using SmartTorque.Core.Entities;

namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Builds the notices shown on the worker board (Architecture §5.3).
/// The SignalR push to the `workers` group lands with the worker board (P5.14);
/// the notice is persisted now so it is already waiting there when it arrives.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// The Black-job notice raised when an import creates urgent jobs (PRD BM-10).
    /// One notice covers the whole import — a sheet with twenty Black jobs should not
    /// bury the board in twenty rows.
    /// <para>
    /// The caller adds the result to its change tracker before saving, so it commits in
    /// the same transaction as the jobs it announces. The jobs must already be tracked
    /// by that context: the notice keeps its link through <see cref="Notification.Job"/>
    /// rather than <see cref="Notification.JobId"/>, because the ids do not exist yet.
    /// </para>
    /// </summary>
    Notification CreateBlackJobNotice(IReadOnlyList<Job> blackJobs);
}