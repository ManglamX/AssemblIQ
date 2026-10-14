using SmartTorque.Core.Entities;
using SmartTorque.Core.Interfaces;

namespace SmartTorque.Services;

/// <summary>
/// Reusable ordering for the workload board (PRD §5.4, P4.7).
/// Sorts by Priority (lower number = more urgent), then DueDate (non-null first, earliest first),
/// then Upload time (via SourceBmRowId, older first).
/// </summary>
public sealed class PriorityService : IPriorityService
{
    public IQueryable<Job> OrderForWorkboard(IQueryable<Job> query)
        => query
            .OrderBy(j => j.Priority)                                          // 1 Black → 5 White
            .ThenBy(j => j.DueDate == null)                                  // non-null dates first (false sorts before true)
            .ThenBy(j => j.DueDate)                                          // earliest date first
            .ThenBy(j => j.SourceBmRowId);                                   // oldest upload first (nulls last in EF)
}