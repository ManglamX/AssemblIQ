using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

public record ExecutionStepDto(
    int StepId,
    int SequenceNo,
    string Label,
    InstrumentType Instrument,
    decimal Target,
    decimal ToleranceMinus,
    decimal TolerancePlus,
    ToleranceMode ToleranceMode,
    decimal LowerLimit,
    decimal UpperLimit,
    int MaxRetries,
    decimal? MarkerX,
    decimal? MarkerY,
    int? MarkerSize,
    bool IsCompleted,
    bool IsCurrent,
    int AttemptCount,
    List<Core.Entities.SessionResult> Attempts
);

public record ExecutionComponentDto(
    int ProductComponentId,
    string ComponentName,
    int AssemblyOrder,
    int Quantity,
    int TemplateId,
    string TemplateCode,
    string TemplateName,
    string? ImagePath,
    TemplateMode TemplateMode,
    int StepCount,
    bool IsCompletedForCurrentUnit,
    bool IsCurrent
);

public record ExecutionUnitDto(
    int UnitIndex,
    string TokenNo,
    UnitAssemblyStatus Status,
    bool IsCurrent,
    int ReworkCount = 0,
    string? LatestReworkReason = null
);

public record ExecutionStateDto(
    int JobId,
    string JobNo,
    PriorityLevel Priority,
    JobStatus Status,
    int Quantity,
    int QuantityCompleted,
    DateOnly? DueDate,
    int ProductId,
    string ProductCode,
    string ProductName,
    string? ProductDescription,
    long SessionId,
    int UnitNo,
    int ComponentInstanceNo,
    SessionStatus SessionStatus,
    Core.Enums.SessionResult SessionFinalResult,
    int CurrentStepSequence,
    ExecutionComponentDto CurrentComponent,
    List<ExecutionComponentDto> Components,
    List<ExecutionStepDto> Steps,
    ExecutionStepDto? CurrentStep,
    string? TorqueDeviceName,
    string? VernierDeviceName,
    string? TorqueDeviceTransmitterId,
    string? VernierDeviceTransmitterId,
    string? UnitTokenNo = null,
    List<ExecutionUnitDto>? Units = null,
    AllocationScopeType ScopeType = AllocationScopeType.FullProduct,
    string? ScopeSummary = null
);

public record ProcessReadingCommand(
    long? SessionId,
    int? DeviceId,
    string? TransmitterId,
    long? DeviceSeq,
    decimal Value,
    InstrumentType InstrumentType,
    bool IsManualEntry = false,
    string? ManualApprovedBy = null,
    string? ReasonCode = null,
    DateTime? CapturedAt = null,
    bool ForceFail = false
);

public record ReadingEvaluationResult(
    bool Success,
    string? ErrorMessage,
    long ResultId,
    long SessionId,
    int StepId,
    int StepSequence,
    string StepLabel,
    decimal ActualValue,
    decimal TargetValue,
    decimal LowerLimit,
    decimal UpperLimit,
    StepResult Result,
    int AttemptNo,
    int MaxRetries,
    bool IsExceededRetries,
    bool IsStepPassed,
    bool IsSessionCompleted,
    bool IsUnitCompleted,
    bool IsJobCompleted,
    int? NextStepSequence,
    int QuantityCompleted,
    int TotalQuantity
);

public record SessionSummaryStepDto(
    int StepId,
    int SequenceNo,
    string Label,
    InstrumentType Instrument,
    decimal Target,
    decimal LowerLimit,
    decimal UpperLimit,
    decimal ActualValue,
    StepResult Result,
    int AttemptNo,
    DateTime CapturedAt,
    bool IsManualEntry,
    string? ReasonCode
);

public record SessionSummaryDto(
    long SessionId,
    int JobId,
    string JobNo,
    int ProductId,
    string ProductCode,
    string ProductName,
    int ProductComponentId,
    string ComponentName,
    string TemplateCode,
    string TemplateName,
    int TemplateVersion,
    int UnitNo,
    int ComponentInstanceNo,
    SessionStatus Status,
    Core.Enums.SessionResult FinalResult,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan Duration,
    string WorkerId,
    string WorkerName,
    string? TorqueDevice,
    string? VernierDevice,
    int JobQuantity,
    int JobQuantityCompleted,
    bool IsJobCompleted,
    int? NextComponentId,
    string? NextComponentName,
    List<SessionSummaryStepDto> Steps
);

public record WorkerHistoryItemDto(
    long SessionId,
    int JobId,
    string JobNo,
    string ProductCode,
    string ComponentName,
    int UnitNo,
    Core.Enums.SessionResult FinalResult,
    SessionStatus Status,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan Duration,
    int PassedSteps,
    int TotalSteps,
    string? AssemblyCode = null,
    string? AssemblyName = null
);

public record WorkerProfileStatsDto(
    string WorkerId,
    string FullName,
    string Email,
    string? EmployeeNo,
    DateTime? JoinedAt,
    int TotalSessions,
    int PassedSessions,
    int FailedSessions,
    decimal PassRate,
    int TotalUnitsCompleted,
    int TodaySessions,
    int TodayPassed
);

public interface ISessionService
{
    Task<Session?> GetActiveSessionForJobAsync(int jobId, string workerId, int? unitNo = null, int? productComponentId = null, CancellationToken ct = default);
    Task<Session> StartOrResumeSessionAsync(int jobId, string workerId, int? productComponentId = null, int? torqueDeviceId = null, int? vernierDeviceId = null, int? unitNo = null, CancellationToken ct = default);
    Task<bool> PairDevicesAsync(long sessionId, int? torqueDeviceId, int? vernierDeviceId, CancellationToken ct = default);
    Task<ExecutionStateDto> GetExecutionStateAsync(int jobId, string workerId, int? unitNo = null, int? productComponentId = null, CancellationToken ct = default);
    Task<ReadingEvaluationResult> ProcessReadingAsync(ProcessReadingCommand command, CancellationToken ct = default);
    Task<Session> SwitchComponentAsync(int jobId, int productComponentId, string workerId, int? unitNo = null, CancellationToken ct = default);
    Task<Session> SwitchUnitAsync(int jobId, int unitNo, string workerId, int? productComponentId = null, CancellationToken ct = default);
    Task<IReadOnlyList<JobUnit>> GetJobUnitsAsync(int jobId, CancellationToken ct = default);
    Task HoldSessionAsync(long sessionId, string? remarks, CancellationToken ct = default);
    Task<SessionSummaryDto?> GetSessionSummaryAsync(long sessionId, CancellationToken ct = default);
    Task<IReadOnlyList<WorkerHistoryItemDto>> GetWorkerHistoryAsync(string workerId, string? filter = null, CancellationToken ct = default);
    Task<WorkerProfileStatsDto> GetWorkerProfileStatsAsync(string workerId, CancellationToken ct = default);
}

public class SessionService : ISessionService
{
    private readonly AppDbContext _db;
    private readonly IMeasurementEvaluator _evaluator;

    public SessionService(AppDbContext db, IMeasurementEvaluator evaluator)
    {
        _db = db;
        _evaluator = evaluator;
    }

    public async Task<Session?> GetActiveSessionForJobAsync(int jobId, string workerId, int? unitNo = null, int? productComponentId = null, CancellationToken ct = default)
    {
        var query = _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Where(s => s.JobId == jobId &&
                        s.WorkerId == workerId &&
                        s.Status != SessionStatus.Completed);

        if (unitNo.HasValue)
        {
            query = query.Where(s => s.UnitNo == unitNo.Value);
        }

        if (productComponentId.HasValue)
        {
            query = query.Where(s => s.ProductComponentId == productComponentId.Value);
        }

        // Prefer session with currently open activity window for this worker
        var openSessionId = await _db.SessionActivityWindows
            .Where(w => w.WorkerId == workerId && w.WindowEnd == null && w.Session.JobId == jobId)
            .OrderByDescending(w => w.WindowStart)
            .Select(w => (long?)w.SessionId)
            .FirstOrDefaultAsync(ct);

        if (openSessionId.HasValue && !unitNo.HasValue && !productComponentId.HasValue)
        {
            var activeByWindow = await query.FirstOrDefaultAsync(s => s.SessionId == openSessionId.Value, ct);
            if (activeByWindow != null) return activeByWindow;
        }

        return await query
            .OrderByDescending(s => s.SessionId)
            .FirstOrDefaultAsync(ct);
    }

    private async Task EnsureJobUnitsExistAsync(Job job, CancellationToken ct)
    {
        var existingIndices = await _db.JobUnits
            .Where(u => u.JobId == job.JobId)
            .Select(u => u.UnitIndex)
            .ToListAsync(ct);

        if (existingIndices.Count < job.Quantity)
        {
            for (int i = 1; i <= job.Quantity; i++)
            {
                if (!existingIndices.Contains(i))
                {
                    _db.JobUnits.Add(new JobUnit
                    {
                        JobId = job.JobId,
                        UnitIndex = i,
                        TokenNo = TokenGenerator.GenerateProductUnitToken(),
                        Status = i <= job.QuantityCompleted ? UnitAssemblyStatus.Completed : UnitAssemblyStatus.Pending,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<JobUnit>> GetJobUnitsAsync(int jobId, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.JobId == jobId, ct);
        if (job == null) return Array.Empty<JobUnit>();

        await EnsureJobUnitsExistAsync(job, ct);

        return await _db.JobUnits
            .Where(u => u.JobId == jobId)
            .OrderBy(u => u.UnitIndex)
            .ToListAsync(ct);
    }

    public async Task<Session> StartOrResumeSessionAsync(int jobId, string workerId, int? productComponentId = null, int? torqueDeviceId = null, int? vernierDeviceId = null, int? unitNo = null, CancellationToken ct = default)
    {
        var job = await _db.Jobs
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot start job in '{job.Status}' status.");
        }

        await EnsureJobUnitsExistAsync(job, ct);

        // Assign job to worker if unassigned
        if (string.IsNullOrWhiteSpace(job.AssignedWorkerId))
        {
            job.AssignedWorkerId = workerId;
        }

        if (job.Status != JobStatus.InProgress)
        {
            job.Status = JobStatus.InProgress;
            job.UpdatedAt = DateTime.UtcNow;
        }

        // 1. Close open activity windows for this worker or newly bound devices
        var openWindows = await _db.SessionActivityWindows
            .Where(w => w.WindowEnd == null && (w.WorkerId == workerId || (torqueDeviceId.HasValue && w.DeviceId == torqueDeviceId.Value)))
            .ToListAsync(ct);
        foreach (var w in openWindows)
        {
            w.WindowEnd = DateTime.UtcNow;
        }

        // 2. If a specific device is selected, unbind it from other sessions without abandoning them
        if (torqueDeviceId.HasValue && torqueDeviceId.Value > 0)
        {
            var otherTorqueSessions = await _db.Sessions
                .Where(s => s.TorqueDeviceId == torqueDeviceId.Value)
                .ToListAsync(ct);
            foreach (var stale in otherTorqueSessions)
            {
                stale.TorqueDeviceId = null;
            }
        }
        if (vernierDeviceId.HasValue && vernierDeviceId.Value > 0)
        {
            var otherVernierSessions = await _db.Sessions
                .Where(s => s.VernierDeviceId == vernierDeviceId.Value)
                .ToListAsync(ct);
            foreach (var stale in otherVernierSessions)
            {
                stale.VernierDeviceId = null;
            }
        }

        // Look for existing active session for this worker and job
        var activeSession = await _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(s => s.JobId == jobId &&
                                      s.WorkerId == workerId &&
                                      s.Status != SessionStatus.Completed, ct);

        if (activeSession != null)
        {
            bool needSwitch = false;
            if (productComponentId.HasValue && activeSession.ProductComponentId != productComponentId.Value)
            {
                needSwitch = true;
            }
            if (unitNo.HasValue && activeSession.UnitNo != unitNo.Value)
            {
                needSwitch = true;
            }

            if (needSwitch)
            {
                // Pause active session without abandoning it
                activeSession.TorqueDeviceId = null;
                activeSession.VernierDeviceId = null;
                activeSession = null;
            }
            else
            {
                if (torqueDeviceId.HasValue && torqueDeviceId.Value > 0)
                    activeSession.TorqueDeviceId = torqueDeviceId.Value;
                if (vernierDeviceId.HasValue && vernierDeviceId.Value > 0)
                    activeSession.VernierDeviceId = vernierDeviceId.Value;

                _db.SessionActivityWindows.Add(new SessionActivityWindow
                {
                    SessionId = activeSession.SessionId,
                    DeviceId = activeSession.TorqueDeviceId,
                    WorkerId = workerId,
                    WindowStart = DateTime.UtcNow
                });

                await _db.SaveChangesAsync(ct);
                return activeSession;
            }
        }

        // Determine target unit
        int targetUnitNo;
        if (unitNo.HasValue && unitNo.Value >= 1 && unitNo.Value <= job.Quantity)
        {
            targetUnitNo = unitNo.Value;
        }
        else
        {
            var pendingUnit = await _db.JobUnits
                .Where(u => u.JobId == jobId && u.Status != UnitAssemblyStatus.Completed)
                .OrderBy(u => u.UnitIndex)
                .FirstOrDefaultAsync(ct);

            targetUnitNo = pendingUnit?.UnitIndex ?? Math.Min(job.QuantityCompleted + 1, job.Quantity);
        }

        // Mark JobUnit in progress if pending
        var jobUnit = await _db.JobUnits.FirstOrDefaultAsync(u => u.JobId == jobId && u.UnitIndex == targetUnitNo, ct);
        if (jobUnit != null && jobUnit.Status == UnitAssemblyStatus.Pending)
        {
            jobUnit.Status = UnitAssemblyStatus.InProgress;
        }

        // Determine target component - scoped to job allowed templates
        var allowedComponents = job.Product.Components
            .Where(c => job.IsTemplateAllowed(c.TemplateId))
            .OrderBy(c => c.AssemblyOrder)
            .ToList();
        if (allowedComponents.Count == 0)
        {
            allowedComponents = job.Product.Components.OrderBy(c => c.AssemblyOrder).ToList();
        }

        ProductComponent? component = null;
        if (productComponentId.HasValue)
        {
            component = allowedComponents.FirstOrDefault(c => c.ProductComponentId == productComponentId.Value);
        }

        if (component == null)
        {
            var passingStepIds = await _db.SessionResults
                .Where(r => r.Session.JobId == jobId &&
                            r.Session.UnitNo == targetUnitNo &&
                            r.Result == StepResult.Pass)
                .Select(r => r.StepId)
                .Distinct()
                .ToListAsync(ct);

            // Find first allowed component that has unfinished steps within this job's scope
            component = allowedComponents
                .FirstOrDefault(c => c.Template.Steps.Any(s => job.IsStepAllowed(s.TemplateStepId) && !passingStepIds.Contains(s.TemplateStepId)))
                ?? allowedComponents.First();
        }

        // Find devices: either explicitly chosen or defaults
        Device? torqueDevice = null;
        if (torqueDeviceId.HasValue && torqueDeviceId.Value > 0)
        {
            torqueDevice = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == torqueDeviceId.Value, ct);
        }
        torqueDevice ??= await _db.Devices
            .Where(d => d.InstrumentType == InstrumentType.Torque && d.IsActive)
            .OrderBy(d => d.DeviceId)
            .FirstOrDefaultAsync(ct);

        Device? vernierDevice = null;
        if (vernierDeviceId.HasValue && vernierDeviceId.Value > 0)
        {
            vernierDevice = await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == vernierDeviceId.Value, ct);
        }
        vernierDevice ??= await _db.Devices
            .Where(d => d.InstrumentType == InstrumentType.Vernier && d.IsActive)
            .OrderBy(d => d.DeviceId)
            .FirstOrDefaultAsync(ct);

        // Check if an InProgress session already exists for this unit and component
        var existingInProgress = await _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(s => s.JobId == jobId &&
                                      s.ProductComponentId == component.ProductComponentId &&
                                      s.UnitNo == targetUnitNo &&
                                      s.ComponentInstanceNo == 1 &&
                                      s.Status != SessionStatus.Completed, ct);

        // Check if a completed session already exists for this unit and component
        var existingCompleted = await _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Where(s => s.JobId == jobId &&
                        s.ProductComponentId == component.ProductComponentId &&
                        s.UnitNo == targetUnitNo &&
                        s.ComponentInstanceNo == 1 &&
                        s.Status == SessionStatus.Completed)
            .OrderByDescending(s => s.SessionId)
            .FirstOrDefaultAsync(ct);

        if (existingInProgress != null)
        {
            // If there's an empty in-progress session but this component is already completed and passed, use the completed session
            if (existingCompleted != null && existingCompleted.FinalResult == Core.Enums.SessionResult.Pass &&
                !await _db.SessionResults.AnyAsync(r => r.SessionId == existingInProgress.SessionId, ct))
            {
                var windows = await _db.SessionActivityWindows.Where(w => w.SessionId == existingInProgress.SessionId).ToListAsync(ct);
                _db.SessionActivityWindows.RemoveRange(windows);
                _db.Sessions.Remove(existingInProgress);
                await _db.SaveChangesAsync(ct);
                return existingCompleted;
            }

            existingInProgress.WorkerId = workerId;
            if (torqueDevice != null) existingInProgress.TorqueDeviceId = torqueDevice.DeviceId;
            if (vernierDevice != null) existingInProgress.VernierDeviceId = vernierDevice.DeviceId;

            _db.SessionActivityWindows.Add(new SessionActivityWindow
            {
                SessionId = existingInProgress.SessionId,
                DeviceId = torqueDevice?.DeviceId,
                WorkerId = workerId,
                WindowStart = DateTime.UtcNow
            });

            await _db.SaveChangesAsync(ct);
            return existingInProgress;
        }

        if (existingCompleted != null)
        {
            var allowedStepIds = component.Template.Steps
                .Where(s => job.IsStepAllowed(s.TemplateStepId))
                .Select(s => s.TemplateStepId)
                .ToList();

            var passedStepIds = await _db.SessionResults
                .Where(r => r.Session.JobId == jobId &&
                            r.Session.UnitNo == targetUnitNo &&
                            r.Result == StepResult.Pass)
                .Select(r => r.StepId)
                .Distinct()
                .ToListAsync(ct);

            bool hasUnfinishedAllowedSteps = allowedStepIds.Any(sId => !passedStepIds.Contains(sId));
            if (!hasUnfinishedAllowedSteps)
            {
                return existingCompleted;
            }
            // There are unfinished steps in this scope! Create a new session to complete remaining steps.
        }

        var allowedStepsForScope = component.Template.Steps
            .Where(s => job.IsStepAllowed(s.TemplateStepId))
            .OrderBy(s => s.SequenceNo)
            .ToList();

        var priorPassedStepIds = await _db.SessionResults
            .Where(r => r.Session.JobId == jobId &&
                        r.Session.UnitNo == targetUnitNo &&
                        r.Result == StepResult.Pass)
            .Select(r => r.StepId)
            .Distinct()
            .ToListAsync(ct);

        var firstRemainingStep = allowedStepsForScope
            .FirstOrDefault(s => !priorPassedStepIds.Contains(s.TemplateStepId));

        var initialSeq = firstRemainingStep?.SequenceNo ?? allowedStepsForScope.FirstOrDefault()?.SequenceNo ?? 1;

        var newSession = new Session
        {
            JobId = jobId,
            ProductComponentId = component.ProductComponentId,
            UnitNo = targetUnitNo,
            ComponentInstanceNo = 1,
            WorkerId = workerId,
            TorqueDeviceId = torqueDevice?.DeviceId,
            VernierDeviceId = vernierDevice?.DeviceId,
            StartTime = DateTime.UtcNow,
            Status = SessionStatus.InProgress,
            FinalResult = Core.Enums.SessionResult.Incomplete,
            CurrentStepSequence = initialSeq
        };

        _db.Sessions.Add(newSession);
        await _db.SaveChangesAsync(ct);

        _db.SessionActivityWindows.Add(new SessionActivityWindow
        {
            SessionId = newSession.SessionId,
            DeviceId = torqueDevice?.DeviceId,
            WorkerId = workerId,
            WindowStart = DateTime.UtcNow
        });
        await _db.SaveChangesAsync(ct);

        return newSession;
    }

    public async Task<bool> PairDevicesAsync(long sessionId, int? torqueDeviceId, int? vernierDeviceId, CancellationToken ct = default)
    {
        var session = await _db.Sessions.FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session == null) return false;

        // When a torque device is selected, ensure EXCLUSIVE ownership by this session:
        // unbind from all other sessions and abandon any lingering in-progress sessions.
        if (torqueDeviceId.HasValue)
        {
            if (torqueDeviceId.Value > 0)
            {
                var targetTorqueId = torqueDeviceId.Value;
                var otherSessions = await _db.Sessions
                    .Where(s => s.SessionId != sessionId && s.TorqueDeviceId == targetTorqueId)
                    .ToListAsync(ct);

                foreach (var oldSess in otherSessions)
                {
                    oldSess.TorqueDeviceId = null;
                }

                session.TorqueDeviceId = targetTorqueId;
                session.Status = SessionStatus.InProgress;
                session.StartTime = DateTime.UtcNow;
            }
            else
            {
                session.TorqueDeviceId = null;
            }
        }

        // When a vernier device is selected, ensure exclusive ownership
        if (vernierDeviceId.HasValue)
        {
            if (vernierDeviceId.Value > 0)
            {
                var targetVernierId = vernierDeviceId.Value;
                var otherSessions = await _db.Sessions
                    .Where(s => s.SessionId != sessionId && s.VernierDeviceId == targetVernierId)
                    .ToListAsync(ct);

                foreach (var oldSess in otherSessions)
                {
                    oldSess.VernierDeviceId = null;
                    if (oldSess.Status == SessionStatus.InProgress)
                    {
                        oldSess.Status = SessionStatus.Abandoned;
                        oldSess.EndTime = DateTime.UtcNow;
                    }
                }

                session.VernierDeviceId = targetVernierId;
                session.Status = SessionStatus.InProgress;
                session.StartTime = DateTime.UtcNow;
            }
            else
            {
                session.VernierDeviceId = null;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<Session> SwitchComponentAsync(int jobId, int productComponentId, string workerId, int? unitNo = null, CancellationToken ct = default)
    {
        var openWindows = await _db.SessionActivityWindows
            .Where(w => w.WorkerId == workerId && w.WindowEnd == null && w.Session.JobId == jobId)
            .ToListAsync(ct);
        foreach (var w in openWindows) w.WindowEnd = DateTime.UtcNow;

        if (!unitNo.HasValue)
        {
            var curSess = await _db.Sessions
                .Where(s => s.JobId == jobId && s.WorkerId == workerId && s.Status != SessionStatus.Completed)
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(ct);
            unitNo = curSess?.UnitNo ?? 1;
        }

        await _db.SaveChangesAsync(ct);

        return await StartOrResumeSessionAsync(jobId, workerId, productComponentId: productComponentId, unitNo: unitNo, ct: ct);
    }

    public async Task<Session> SwitchUnitAsync(int jobId, int unitNo, string workerId, int? productComponentId = null, CancellationToken ct = default)
    {
        var openWindows = await _db.SessionActivityWindows
            .Where(w => w.WorkerId == workerId && w.WindowEnd == null && w.Session.JobId == jobId)
            .ToListAsync(ct);
        foreach (var w in openWindows) w.WindowEnd = DateTime.UtcNow;

        if (!productComponentId.HasValue)
        {
            var curSess = await _db.Sessions
                .Where(s => s.JobId == jobId && s.WorkerId == workerId && s.Status != SessionStatus.Completed)
                .OrderByDescending(s => s.SessionId)
                .FirstOrDefaultAsync(ct);
            productComponentId = curSess?.ProductComponentId;
        }

        await _db.SaveChangesAsync(ct);

        return await StartOrResumeSessionAsync(jobId, workerId, productComponentId: productComponentId, unitNo: unitNo, ct: ct);
    }

    public async Task HoldSessionAsync(long sessionId, string? remarks, CancellationToken ct = default)
    {
        var session = await _db.Sessions.Include(s => s.Job).FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);
        if (session != null)
        {
            session.Status = SessionStatus.OnHold;
            session.Remarks = remarks;
            session.Job.Status = JobStatus.OnHold;
            session.Job.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<ExecutionStateDto> GetExecutionStateAsync(int jobId, string workerId, int? unitNo = null, int? productComponentId = null, CancellationToken ct = default)
    {
        var job = await _db.Jobs
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        await EnsureJobUnitsExistAsync(job, ct);

        // 1. Try to find active in-progress session
        var session = await GetActiveSessionForJobAsync(jobId, workerId, unitNo, productComponentId, ct);

        // 2. If unitNo was explicitly specified, or if all active sessions are completed for this unit, check if a completed session exists
        if (session == null && unitNo.HasValue)
        {
            var unitObj = await _db.JobUnits.FirstOrDefaultAsync(u => u.JobId == jobId && u.UnitIndex == unitNo.Value, ct);
            if (unitObj != null && unitObj.Status == UnitAssemblyStatus.Rework)
            {
                // Unit is flagged for REWORK: do NOT pick up completed sessions!
                session = await StartOrResumeSessionAsync(jobId, workerId, productComponentId, unitNo: unitNo, ct: ct);
                if (session.Status == SessionStatus.Completed)
                {
                    session.Status = SessionStatus.InProgress;
                    session.FinalResult = SmartTorque.Core.Enums.SessionResult.Incomplete;
                    session.CurrentStepSequence = 1;
                    session.ReworkCount = unitObj.ReworkCount;
                    session.EndTime = null;
                    await _db.SaveChangesAsync(ct);
                }
            }
            else
            {
                var priorPassedStepIds = await _db.SessionResults
                    .Where(r => r.Session.JobId == jobId &&
                                r.Session.UnitNo == unitNo.Value &&
                                r.Result == StepResult.Pass)
                    .Select(r => r.StepId)
                    .Distinct()
                    .ToListAsync(ct);

                var productSteps = job.Product.Components
                    .Where(c => job.IsTemplateAllowed(c.TemplateId))
                    .SelectMany(c => c.Template.Steps)
                    .Where(s => job.IsStepAllowed(s.TemplateStepId))
                    .Select(s => s.TemplateStepId)
                    .ToList();

                bool isScopeFullyPassed = productSteps.Count > 0 && productSteps.All(sId => priorPassedStepIds.Contains(sId));
                if (isScopeFullyPassed)
                {
                    session = await _db.Sessions
                        .Include(s => s.ProductComponent)
                            .ThenInclude(c => c.Template)
                                .ThenInclude(t => t.Steps)
                        .Include(s => s.TorqueDevice)
                        .Include(s => s.VernierDevice)
                        .Where(s => s.JobId == jobId &&
                                    s.UnitNo == unitNo.Value &&
                                    (productComponentId == null || s.ProductComponentId == productComponentId.Value) &&
                                    s.Status == SessionStatus.Completed)
                        .OrderByDescending(s => s.SessionId)
                        .FirstOrDefaultAsync(ct);
                }
            }
        }

        // 3. Otherwise resume or start session on target unit
        session ??= await StartOrResumeSessionAsync(jobId, workerId, productComponentId, unitNo: unitNo, ct: ct);

        // If current session is for a component whose allowed steps have all already passed on this unit, advance to the next unfinished component
        if (session.Status != SessionStatus.Completed)
        {
            var priorPassed = await _db.SessionResults
                .Where(r => r.Session.JobId == jobId && r.Session.UnitNo == session.UnitNo && r.Result == StepResult.Pass)
                .Select(r => r.StepId)
                .Distinct()
                .ToListAsync(ct);

            var currentCompSteps = session.ProductComponent.Template.Steps
                .Where(s => job.IsStepAllowed(s.TemplateStepId))
                .ToList();

            bool isCurrentCompDone = currentCompSteps.Count > 0 && currentCompSteps.All(s => priorPassed.Contains(s.TemplateStepId));
            if (isCurrentCompDone)
            {
                var nextComp = job.Product.Components
                    .Where(c => job.IsTemplateAllowed(c.TemplateId))
                    .OrderBy(c => c.AssemblyOrder)
                    .FirstOrDefault(c => c.Template.Steps.Any(s => job.IsStepAllowed(s.TemplateStepId) && !priorPassed.Contains(s.TemplateStepId)));

                if (nextComp != null && nextComp.ProductComponentId != session.ProductComponentId)
                {
                    session.Status = SessionStatus.Completed;
                    session.EndTime = DateTime.UtcNow;
                    session.FinalResult = Core.Enums.SessionResult.Pass;
                    await _db.SaveChangesAsync(ct);

                    session = await StartOrResumeSessionAsync(jobId, workerId, productComponentId: nextComp.ProductComponentId, unitNo: session.UnitNo, ct: ct);
                }
            }
        }

        // Fetch completed component IDs for this unit
        var completedComponentIds = await _db.Sessions
            .Where(s => s.JobId == jobId &&
                        s.UnitNo == session.UnitNo &&
                        s.FinalResult == Core.Enums.SessionResult.Pass)
            .Select(s => s.ProductComponentId)
            .Distinct()
            .ToListAsync(ct);

        var currentComponent = job.Product.Components
            .First(c => c.ProductComponentId == session.ProductComponentId);

        var templateSteps = currentComponent.Template.Steps
            .Where(s => job.IsStepAllowed(s.TemplateStepId))
            .OrderBy(s => s.SequenceNo).ToList();

        // Load all session results for this session
        var sessionResults = await _db.SessionResults
            .Where(r => r.SessionId == session.SessionId)
            .OrderBy(r => r.ReceivedAt)
            .ToListAsync(ct);

        var passedStepIdsForUnit = await _db.SessionResults
            .Where(r => r.Session.JobId == jobId &&
                        r.Session.UnitNo == session.UnitNo &&
                        r.Result == StepResult.Pass)
            .Select(r => r.StepId)
            .Distinct()
            .ToListAsync(ct);

        var stepDtos = new List<ExecutionStepDto>();
        foreach (var s in templateSteps)
        {
            var attempts = sessionResults.Where(r => r.StepId == s.TemplateStepId).ToList();
            var hasPassed = attempts.Any(r => r.Result == StepResult.Pass) || passedStepIdsForUnit.Contains(s.TemplateStepId);
            var isCurrent = s.SequenceNo == session.CurrentStepSequence;
            var (lower, upper) = _evaluator.ComputeBand(s.Target, s.ToleranceMinus, s.TolerancePlus, s.ToleranceMode);

            stepDtos.Add(new ExecutionStepDto(
                StepId: s.TemplateStepId,
                SequenceNo: s.SequenceNo,
                Label: s.Label,
                Instrument: s.Instrument,
                Target: s.Target,
                ToleranceMinus: s.ToleranceMinus,
                TolerancePlus: s.TolerancePlus,
                ToleranceMode: s.ToleranceMode,
                LowerLimit: lower,
                UpperLimit: upper,
                MaxRetries: s.MaxRetries,
                MarkerX: s.MarkerX,
                MarkerY: s.MarkerY,
                MarkerSize: s.MarkerSize,
                IsCompleted: hasPassed,
                IsCurrent: isCurrent,
                AttemptCount: attempts.Count,
                Attempts: attempts
            ));
        }

        var currentStepDto = stepDtos.FirstOrDefault(s => s.IsCurrent)
            ?? stepDtos.FirstOrDefault(s => !s.IsCompleted)
            ?? stepDtos.LastOrDefault();

        if (currentStepDto != null && session.CurrentStepSequence != currentStepDto.SequenceNo)
        {
            session.CurrentStepSequence = currentStepDto.SequenceNo;
            await _db.SaveChangesAsync(ct);
        }

        var scopedComponents = job.Product.Components
            .Where(c => job.IsTemplateAllowed(c.TemplateId))
            .OrderBy(c => c.AssemblyOrder)
            .ToList();
        if (scopedComponents.Count == 0) scopedComponents = job.Product.Components.OrderBy(c => c.AssemblyOrder).ToList();

        var componentDtos = scopedComponents.Select(c => new ExecutionComponentDto(
            ProductComponentId: c.ProductComponentId,
            ComponentName: c.ComponentName,
            AssemblyOrder: c.AssemblyOrder,
            Quantity: c.Quantity,
            TemplateId: c.TemplateId,
            TemplateCode: c.Template.TemplateCode,
            TemplateName: c.Template.Name,
            ImagePath: c.Template.ImagePath,
            TemplateMode: c.Template.Mode,
            StepCount: c.Template.Steps.Count(s => job.IsStepAllowed(s.TemplateStepId)),
            IsCompletedForCurrentUnit: completedComponentIds.Contains(c.ProductComponentId),
            IsCurrent: c.ProductComponentId == currentComponent.ProductComponentId
        )).ToList();

        var torqueDev = session.TorqueDeviceId.HasValue
            ? await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == session.TorqueDeviceId.Value, ct)
            : null;

        var vernierDev = session.VernierDeviceId.HasValue
            ? await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == session.VernierDeviceId.Value, ct)
            : null;

        var jobUnits = await GetJobUnitsAsync(job.JobId, ct);
        var currentUnit = jobUnits.FirstOrDefault(u => u.UnitIndex == session.UnitNo);
        var unitDtos = jobUnits.Select(u => new ExecutionUnitDto(
            u.UnitIndex,
            u.TokenNo,
            u.Status,
            u.UnitIndex == session.UnitNo,
            u.ReworkCount,
            u.LatestReworkReason
        )).ToList();

        return new ExecutionStateDto(
            JobId: job.JobId,
            JobNo: job.JobNo,
            Priority: job.Priority,
            Status: job.Status,
            Quantity: job.Quantity,
            QuantityCompleted: job.QuantityCompleted,
            DueDate: job.DueDate,
            ProductId: job.ProductId,
            ProductCode: job.Product.ProductCode,
            ProductName: job.Product.ProductName,
            ProductDescription: job.Product.Description,
            SessionId: session.SessionId,
            UnitNo: session.UnitNo,
            ComponentInstanceNo: session.ComponentInstanceNo,
            SessionStatus: session.Status,
            SessionFinalResult: session.FinalResult,
            CurrentStepSequence: session.CurrentStepSequence,
            CurrentComponent: componentDtos.First(c => c.ProductComponentId == currentComponent.ProductComponentId),
            Components: componentDtos,
            Steps: stepDtos,
            CurrentStep: currentStepDto,
            TorqueDeviceName: torqueDev?.Name,
            VernierDeviceName: vernierDev?.Name,
            TorqueDeviceTransmitterId: torqueDev?.TransmitterId,
            VernierDeviceTransmitterId: vernierDev?.TransmitterId,
            UnitTokenNo: currentUnit?.TokenNo ?? $"UNIT-{session.UnitNo}",
            Units: unitDtos,
            ScopeType: job.ScopeType,
            ScopeSummary: job.ScopeSummary
        );
    }

    public async Task<ReadingEvaluationResult> ProcessReadingAsync(ProcessReadingCommand command, CancellationToken ct = default)
    {
        Session? session = null;

        if (command.SessionId.HasValue)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
                    .ThenInclude(j => j.Product)
                        .ThenInclude(p => p.Components)
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.SessionId == command.SessionId.Value, ct);
        }
        else if (command.DeviceId.HasValue)
        {
            // Find active in-progress session referencing this device
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
                                         (s.TorqueDeviceId == command.DeviceId.Value || s.VernierDeviceId == command.DeviceId.Value), ct);
        }

        if (session == null)
        {
            // Fallback: active session on an active in-progress job
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
        }

        if (session == null)
        {
            return new ReadingEvaluationResult(
                Success: false,
                ErrorMessage: "No active in-progress session found for this reading.",
                ResultId: 0,
                SessionId: 0,
                StepId: 0,
                StepSequence: 0,
                StepLabel: string.Empty,
                ActualValue: command.Value,
                TargetValue: 0,
                LowerLimit: 0,
                UpperLimit: 0,
                Result: StepResult.Fail,
                AttemptNo: 0,
                MaxRetries: 0,
                IsExceededRetries: false,
                IsStepPassed: false,
                IsSessionCompleted: false,
                IsUnitCompleted: false,
                IsJobCompleted: false,
                NextStepSequence: null,
                QuantityCompleted: 0,
                TotalQuantity: 0
            );
        }

        // Deduplication disabled: every measurement is evaluated and recorded

        var steps = session.ProductComponent.Template.Steps
            .Where(s => session.Job.IsStepAllowed(s.TemplateStepId))
            .OrderBy(s => s.SequenceNo).ToList();
        var currentStep = steps.FirstOrDefault(s => s.SequenceNo == session.CurrentStepSequence)
            ?? steps.FirstOrDefault();

        if (currentStep == null)
        {
            return new ReadingEvaluationResult(
                Success: false,
                ErrorMessage: "Template contains no measurement steps.",
                ResultId: 0,
                SessionId: session.SessionId,
                StepId: 0,
                StepSequence: 0,
                StepLabel: string.Empty,
                ActualValue: command.Value,
                TargetValue: 0,
                LowerLimit: 0,
                UpperLimit: 0,
                Result: StepResult.Fail,
                AttemptNo: 0,
                MaxRetries: 0,
                IsExceededRetries: false,
                IsStepPassed: false,
                IsSessionCompleted: false,
                IsUnitCompleted: false,
                IsJobCompleted: false,
                NextStepSequence: null,
                QuantityCompleted: session.Job.QuantityCompleted,
                TotalQuantity: session.Job.Quantity
            );
        }

        var (lower, upper) = _evaluator.ComputeBand(currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);

        if (session.Status == SessionStatus.Completed)
        {
            return new ReadingEvaluationResult(
                Success: true,
                ErrorMessage: null,
                ResultId: 0,
                SessionId: session.SessionId,
                StepId: currentStep.TemplateStepId,
                StepSequence: currentStep.SequenceNo,
                StepLabel: currentStep.Label,
                ActualValue: command.Value,
                TargetValue: currentStep.Target,
                LowerLimit: lower,
                UpperLimit: upper,
                Result: StepResult.Pass,
                AttemptNo: 1,
                MaxRetries: currentStep.MaxRetries,
                IsExceededRetries: false,
                IsStepPassed: true,
                IsSessionCompleted: true,
                IsUnitCompleted: true,
                IsJobCompleted: session.Job.Status == JobStatus.Completed,
                NextStepSequence: null,
                QuantityCompleted: session.Job.QuantityCompleted,
                TotalQuantity: session.Job.Quantity
            );
        }

        bool isDevOrUniversal = (command.TransmitterId != null && command.TransmitterId.StartsWith("esp", StringComparison.OrdinalIgnoreCase)) ||
                                (command.TransmitterId != null && command.TransmitterId.StartsWith("SIM", StringComparison.OrdinalIgnoreCase));
        if (command.InstrumentType != currentStep.Instrument && !isDevOrUniversal)
        {
            var attemptsCount = await _db.SessionResults
                .CountAsync(r => r.SessionId == session.SessionId && r.StepId == currentStep.TemplateStepId, ct);

            return new ReadingEvaluationResult(
                Success: false,
                ErrorMessage: "UNEXPECTED_INSTRUMENT",
                ResultId: 0,
                SessionId: session.SessionId,
                StepId: currentStep.TemplateStepId,
                StepSequence: currentStep.SequenceNo,
                StepLabel: currentStep.Label,
                ActualValue: command.Value,
                TargetValue: currentStep.Target,
                LowerLimit: lower,
                UpperLimit: upper,
                Result: StepResult.Fail,
                AttemptNo: attemptsCount,
                MaxRetries: currentStep.MaxRetries,
                IsExceededRetries: false,
                IsStepPassed: false,
                IsSessionCompleted: false,
                IsUnitCompleted: false,
                IsJobCompleted: false,
                NextStepSequence: session.CurrentStepSequence,
                QuantityCompleted: session.Job.QuantityCompleted,
                TotalQuantity: session.Job.Quantity
            );
        }

        bool isPass;
        StepResult stepResult;

        if (command.ForceFail)
        {
            isPass = false;
            stepResult = StepResult.Fail;
        }
        else
        {
            isPass = _evaluator.IsInBand(command.Value, currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);
            stepResult = isPass ? StepResult.Pass : StepResult.Fail;
        }

        var existingAttemptsCount = await _db.SessionResults
            .CountAsync(r => r.SessionId == session.SessionId && r.StepId == currentStep.TemplateStepId, ct);
        var attemptNo = existingAttemptsCount + 1;

        var resultRecord = new Core.Entities.SessionResult
        {
            SessionId = session.SessionId,
            StepId = currentStep.TemplateStepId,
            InstrumentType = command.InstrumentType,
            DeviceId = command.DeviceId,
            AttemptNo = attemptNo,
            TargetValue = currentStep.Target,
            TolerancePlus = currentStep.TolerancePlus,
            ToleranceMinus = currentStep.ToleranceMinus,
            ToleranceMode = currentStep.ToleranceMode,
            LowerLimit = lower,
            UpperLimit = upper,
            ActualValue = command.Value,
            Result = stepResult,
            IsManualEntry = command.IsManualEntry,
            ManualApprovedBy = command.ManualApprovedBy,
            ReasonCode = command.ForceFail ? (command.ReasonCode ?? "OPERATOR_FORCE_FAIL") : command.ReasonCode,
            DeviceSeq = command.DeviceSeq,
            CapturedAt = command.CapturedAt ?? DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        _db.SessionResults.Add(resultRecord);

        bool isSessionCompleted = false;
        bool isUnitCompleted = false;
        bool isJobCompleted = false;
        int? nextStepSequence = session.CurrentStepSequence;
        bool isExceededRetries = false; // Unlimited retries supported

        if (isPass || command.ForceFail)
        {
            var nextStep = steps.FirstOrDefault(s => s.SequenceNo > currentStep.SequenceNo);
            if (nextStep != null)
            {
                session.CurrentStepSequence = nextStep.SequenceNo;
                nextStepSequence = nextStep.SequenceNo;
            }
            else
            {
                // All steps completed for this session!
                session.Status = SessionStatus.Completed;
                session.EndTime = DateTime.UtcNow;
                isSessionCompleted = true;

                var sessionResultsForComponent = await _db.SessionResults
                    .Where(r => r.SessionId == session.SessionId)
                    .ToListAsync(ct);

                bool hasUnpassedStep = steps.Any(st =>
                {
                    if (st.TemplateStepId == currentStep.TemplateStepId)
                    {
                        return !isPass;
                    }
                    return !sessionResultsForComponent.Any(r => r.StepId == st.TemplateStepId && r.Result == StepResult.Pass);
                });

                session.FinalResult = hasUnpassedStep ? Core.Enums.SessionResult.Fail : Core.Enums.SessionResult.Pass;

                var jobUnit = await _db.JobUnits.FirstOrDefaultAsync(u => u.JobId == session.JobId && u.UnitIndex == session.UnitNo, ct);
                if (hasUnpassedStep)
                {
                    if (jobUnit != null) jobUnit.Status = UnitAssemblyStatus.Failed;
                    session.Job.Status = JobStatus.Rework;
                    session.Job.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // Check if ALL required steps across all scoped components for this unit are complete with Pass
                    var product = session.Job.Product ?? await _db.Products
                        .Include(p => p.Components)
                        .FirstOrDefaultAsync(p => p.ProductId == session.Job.ProductId, ct);

                    var scopedComps = product?.Components
                        .Where(c => session.Job.IsTemplateAllowed(c.TemplateId))
                        .ToList() ?? new List<SmartTorque.Core.Entities.ProductComponent>();

                    var scopedTemplateIds = scopedComps.Select(c => c.TemplateId).ToList();
                    var rawStepIds = await _db.TemplateSteps
                        .Where(ts => scopedTemplateIds.Contains(ts.TemplateId))
                        .Select(ts => ts.TemplateStepId)
                        .ToListAsync(ct);

                    var allRequiredStepIds = rawStepIds
                        .Where(id => session.Job.IsStepAllowed(id))
                        .ToList();

                    var passedStepIdsForUnit = await _db.SessionResults
                        .Where(r => r.Session.JobId == session.JobId &&
                                    r.Session.UnitNo == session.UnitNo &&
                                    r.Result == StepResult.Pass)
                        .Select(r => r.StepId)
                        .Distinct()
                        .ToListAsync(ct);

                    if (isPass && !passedStepIdsForUnit.Contains(currentStep.TemplateStepId))
                    {
                        passedStepIdsForUnit.Add(currentStep.TemplateStepId);
                    }

                    bool allUnitStepsCompleted = allRequiredStepIds.Count > 0 &&
                        allRequiredStepIds.All(sId => passedStepIdsForUnit.Contains(sId));

                    if (allUnitStepsCompleted)
                    {
                        isUnitCompleted = true;
                        if (jobUnit != null)
                        {
                            jobUnit.Status = UnitAssemblyStatus.Completed;
                            jobUnit.CompletedAt = DateTime.UtcNow;
                            jobUnit.CompletedByWorkerId = session.WorkerId;

                            // Complete any active JobUnitRework record for this unit
                            var activeRework = await _db.JobUnitReworks
                                .Where(r => r.JobUnitId == jobUnit.JobUnitId && r.Status == UnitAssemblyStatus.Rework)
                                .OrderByDescending(r => r.ReworkIteration)
                                .FirstOrDefaultAsync(ct);

                            if (activeRework != null)
                            {
                                activeRework.Status = UnitAssemblyStatus.Completed;
                                activeRework.CompletedAt = DateTime.UtcNow;
                                activeRework.CompletedByUserId = session.WorkerId;
                                activeRework.CompletedByUserName = session.Worker?.FullName ?? "Assembly Operator";
                                activeRework.FinalResult = SmartTorque.Core.Enums.SessionResult.Pass;
                                activeRework.ResolutionNotes = "Rework verification passed with all component steps confirmed.";
                            }

                            await _db.SaveChangesAsync(ct);
                        }

                        // Single source of truth: calculate QuantityCompleted directly from fully completed JobUnits
                        var completedUnitsCount = await _db.JobUnits
                            .CountAsync(u => u.JobId == session.JobId && u.Status == UnitAssemblyStatus.Completed, ct);
                        session.Job.QuantityCompleted = completedUnitsCount;

                        if (session.Job.QuantityCompleted >= session.Job.Quantity)
                        {
                            session.Job.Status = JobStatus.Completed;
                            session.Job.UpdatedAt = DateTime.UtcNow;
                            isJobCompleted = true;
                        }
                        else
                        {
                            session.Job.Status = JobStatus.InProgress;
                            session.Job.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    else
                    {
                        // Unit is still partially completed (e.g. current workstation finished 4 of 7 bolts)
                        isUnitCompleted = false;
                        if (jobUnit != null && jobUnit.Status != UnitAssemblyStatus.Rework)
                        {
                            jobUnit.Status = UnitAssemblyStatus.InProgress;
                            jobUnit.CompletedAt = null;
                            jobUnit.CompletedByWorkerId = null;
                            await _db.SaveChangesAsync(ct);
                        }
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);

        return new ReadingEvaluationResult(
            Success: true,
            ErrorMessage: null,
            ResultId: resultRecord.ResultId,
            SessionId: session.SessionId,
            StepId: currentStep.TemplateStepId,
            StepSequence: currentStep.SequenceNo,
            StepLabel: currentStep.Label,
            ActualValue: command.Value,
            TargetValue: currentStep.Target,
            LowerLimit: lower,
            UpperLimit: upper,
            Result: stepResult,
            AttemptNo: attemptNo,
            MaxRetries: currentStep.MaxRetries,
            IsExceededRetries: isExceededRetries,
            IsStepPassed: isPass,
            IsSessionCompleted: isSessionCompleted,
            IsUnitCompleted: isUnitCompleted,
            IsJobCompleted: isJobCompleted,
            NextStepSequence: nextStepSequence,
            QuantityCompleted: session.Job.QuantityCompleted,
            TotalQuantity: session.Job.Quantity
        );
    }

    public async Task<SessionSummaryDto?> GetSessionSummaryAsync(long sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
                    .ThenInclude(p => p.Components)
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Include(s => s.Results)
                .ThenInclude(r => r.Step)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session == null) return null;

        var worker = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == session.WorkerId, ct);

        var templateSteps = session.ProductComponent.Template.Steps
            .Where(st => session.Job.IsStepAllowed(st.TemplateStepId))
            .OrderBy(st => st.SequenceNo).ToList();
        var stepSummaries = new List<SessionSummaryStepDto>();

        foreach (var step in templateSteps)
        {
            var attempts = session.Results.Where(r => r.StepId == step.TemplateStepId).OrderByDescending(r => r.AttemptNo).ToList();
            var passingAttempt = attempts.FirstOrDefault(r => r.Result == StepResult.Pass);
            var result = passingAttempt ?? attempts.FirstOrDefault();

            if (result != null)
            {
                stepSummaries.Add(new SessionSummaryStepDto(
                    StepId: step.TemplateStepId,
                    SequenceNo: step.SequenceNo,
                    Label: step.Label,
                    Instrument: step.Instrument,
                    Target: step.Target,
                    LowerLimit: result.LowerLimit,
                    UpperLimit: result.UpperLimit,
                    ActualValue: result.ActualValue,
                    Result: result.Result,
                    AttemptNo: result.AttemptNo,
                    CapturedAt: result.CapturedAt,
                    IsManualEntry: result.IsManualEntry,
                    ReasonCode: result.ReasonCode
                ));
            }
            else
            {
                var (lower, upper) = _evaluator.ComputeBand(step.Target, step.ToleranceMinus, step.TolerancePlus, step.ToleranceMode);
                stepSummaries.Add(new SessionSummaryStepDto(
                    StepId: step.TemplateStepId,
                    SequenceNo: step.SequenceNo,
                    Label: step.Label,
                    Instrument: step.Instrument,
                    Target: step.Target,
                    LowerLimit: lower,
                    UpperLimit: upper,
                    ActualValue: 0,
                    Result: StepResult.Fail,
                    AttemptNo: 0,
                    CapturedAt: session.StartTime,
                    IsManualEntry: false,
                    ReasonCode: "Not attempted"
                ));
            }
        }

        var components = session.Job.Product.Components.OrderBy(c => c.AssemblyOrder).ToList();
        var currentCompIndex = components.FindIndex(c => c.ProductComponentId == session.ProductComponentId);
        int? nextCompId = null;
        string? nextCompName = null;
        if (currentCompIndex >= 0 && currentCompIndex + 1 < components.Count)
        {
            var nextComp = components[currentCompIndex + 1];
            nextCompId = nextComp.ProductComponentId;
            nextCompName = nextComp.ComponentName;
        }

        var endTime = session.EndTime ?? DateTime.UtcNow;
        var duration = endTime - session.StartTime;

        return new SessionSummaryDto(
            SessionId: session.SessionId,
            JobId: session.JobId,
            JobNo: session.Job.JobNo,
            ProductId: session.Job.ProductId,
            ProductCode: session.Job.Product.ProductCode,
            ProductName: session.Job.Product.ProductName,
            ProductComponentId: session.ProductComponentId,
            ComponentName: session.ProductComponent.ComponentName,
            TemplateCode: session.ProductComponent.Template.TemplateCode,
            TemplateName: session.ProductComponent.Template.Name,
            TemplateVersion: session.ProductComponent.Template.Version,
            UnitNo: session.UnitNo,
            ComponentInstanceNo: session.ComponentInstanceNo,
            Status: session.Status,
            FinalResult: session.FinalResult,
            StartTime: session.StartTime,
            EndTime: session.EndTime,
            Duration: duration,
            WorkerId: session.WorkerId,
            WorkerName: session.Worker?.FullName ?? session.Worker?.UserName ?? session.WorkerId,
            TorqueDevice: session.TorqueDevice?.Name ?? session.TorqueDevice?.TransmitterId,
            VernierDevice: session.VernierDevice?.Name ?? session.VernierDevice?.TransmitterId,
            JobQuantity: session.Job.Quantity,
            JobQuantityCompleted: session.Job.QuantityCompleted,
            IsJobCompleted: session.Job.Status == JobStatus.Completed,
            NextComponentId: nextCompId,
            NextComponentName: nextCompName,
            Steps: stepSummaries
        );
    }

    public async Task<IReadOnlyList<WorkerHistoryItemDto>> GetWorkerHistoryAsync(string workerId, string? filter = null, CancellationToken ct = default)
    {
        var query = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.Job)
                .ThenInclude(j => j.AssemblyLine)
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.Results)
            .Where(s => s.WorkerId == workerId && s.Status == SessionStatus.Completed)
            .OrderByDescending(s => s.StartTime)
            .AsQueryable();

        if (string.Equals(filter, "passed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.FinalResult == Core.Enums.SessionResult.Pass);
        }
        else if (string.Equals(filter, "failed", StringComparison.OrdinalIgnoreCase))
        {
            query = query.Where(s => s.FinalResult == Core.Enums.SessionResult.Fail);
        }

        var sessions = await query.Take(100).ToListAsync(ct);

        return sessions.Select(s =>
        {
            var totalSteps = s.ProductComponent?.Template?.Steps?.Count ?? 0;
            var passedSteps = s.Results
                .Where(r => r.Result == StepResult.Pass)
                .Select(r => r.StepId)
                .Distinct()
                .Count();

            var endTime = s.EndTime ?? DateTime.UtcNow;
            var duration = endTime - s.StartTime;

            return new WorkerHistoryItemDto(
                SessionId: s.SessionId,
                JobId: s.JobId,
                JobNo: s.Job?.JobNo ?? $"JOB-{s.JobId}",
                ProductCode: s.Job?.Product?.ProductCode ?? string.Empty,
                ComponentName: s.ProductComponent?.ComponentName ?? "Component",
                UnitNo: s.UnitNo,
                FinalResult: s.FinalResult,
                Status: s.Status,
                StartTime: s.StartTime,
                EndTime: s.EndTime,
                Duration: duration,
                PassedSteps: passedSteps,
                TotalSteps: totalSteps,
                AssemblyCode: s.Job?.AssemblyLine?.AssemblyCode,
                AssemblyName: s.Job?.AssemblyLine?.AssemblyName
            );
        }).ToList();
    }

    public async Task<WorkerProfileStatsDto> GetWorkerProfileStatsAsync(string workerId, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == workerId, ct);
        var sessions = await _db.Sessions
            .AsNoTracking()
            .Where(s => s.WorkerId == workerId && s.Status == SessionStatus.Completed)
            .ToListAsync(ct);

        var total = sessions.Count;
        var passed = sessions.Count(s => s.FinalResult == Core.Enums.SessionResult.Pass);
        var failed = sessions.Count(s => s.FinalResult == Core.Enums.SessionResult.Fail);
        var passRate = total > 0 ? Math.Round((decimal)passed / total * 100m, 1) : 0m;

        var today = DateTime.UtcNow.Date;
        var todaySessions = sessions.Count(s => s.StartTime.Date == today);
        var todayPassed = sessions.Count(s => s.StartTime.Date == today && s.FinalResult == Core.Enums.SessionResult.Pass);

        var unitsCompleted = await _db.Jobs
            .Where(j => j.AssignedWorkerId == workerId)
            .SumAsync(j => (int?)j.QuantityCompleted, ct) ?? 0;

        return new WorkerProfileStatsDto(
            WorkerId: workerId,
            FullName: user?.FullName ?? user?.UserName ?? "Worker",
            Email: user?.Email ?? string.Empty,
            EmployeeNo: user?.EmployeeNo,
            JoinedAt: user?.CreatedAt,
            TotalSessions: total,
            PassedSessions: passed,
            FailedSessions: failed,
            PassRate: passRate,
            TotalUnitsCompleted: unitsCompleted,
            TodaySessions: todaySessions,
            TodayPassed: todayPassed
        );
    }
}