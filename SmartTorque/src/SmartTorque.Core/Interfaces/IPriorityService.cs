using SmartTorque.Core.Entities;

namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Reusable ordering for the workload board (Architecture §5.4, P4.7).
/// Sorts by Priority (lower = more urgent), then DueDate (earlier first),
/// then Upload time (earlier first via SourceBmRowId).
/// </summary>
public interface IPriorityService
{
    /// <summary>Job order: Black → White, earliest due date first, then oldest upload.</summary>
    IQueryable<Job> OrderForWorkboard(IQueryable<Job> query);
}