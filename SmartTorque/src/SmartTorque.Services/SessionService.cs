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
    string? VernierDeviceTransmitterId
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
    DateTime? CapturedAt = null
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

public interface ISessionService
{
    Task<Session?> GetActiveSessionForJobAsync(int jobId, string workerId, CancellationToken ct = default);
    Task<Session> StartOrResumeSessionAsync(int jobId, string workerId, int? productComponentId = null, CancellationToken ct = default);
    Task<ExecutionStateDto> GetExecutionStateAsync(int jobId, string workerId, CancellationToken ct = default);
    Task<ReadingEvaluationResult> ProcessReadingAsync(ProcessReadingCommand command, CancellationToken ct = default);
    Task<Session> SwitchComponentAsync(int jobId, int productComponentId, string workerId, CancellationToken ct = default);
    Task HoldSessionAsync(long sessionId, string? remarks, CancellationToken ct = default);
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

    public async Task<Session?> GetActiveSessionForJobAsync(int jobId, string workerId, CancellationToken ct = default)
    {
        return await _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .FirstOrDefaultAsync(s => s.JobId == jobId &&
                                      s.WorkerId == workerId &&
                                      s.Status == SessionStatus.InProgress, ct);
    }

    public async Task<Session> StartOrResumeSessionAsync(int jobId, string workerId, int? productComponentId = null, CancellationToken ct = default)
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

        // Look for existing active session for this worker and job
        var activeSession = await _db.Sessions
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .FirstOrDefaultAsync(s => s.JobId == jobId &&
                                      s.WorkerId == workerId &&
                                      s.Status == SessionStatus.InProgress, ct);

        if (activeSession != null)
        {
            // If specific component requested and different, switch
            if (productComponentId.HasValue && activeSession.ProductComponentId != productComponentId.Value)
            {
                activeSession.Status = SessionStatus.Abandoned;
                activeSession.EndTime = DateTime.UtcNow;
                activeSession = null;
            }
            else
            {
                await _db.SaveChangesAsync(ct);
                return activeSession;
            }
        }

        // Determine target unit
        var targetUnitNo = Math.Min(job.QuantityCompleted + 1, job.Quantity);

        // Determine target component
        ProductComponent? component = null;
        if (productComponentId.HasValue)
        {
            component = job.Product.Components.FirstOrDefault(c => c.ProductComponentId == productComponentId.Value);
        }

        if (component == null)
        {
            // Find first component that does not have a passing session for this unit
            var completedComponentIds = await _db.Sessions
                .Where(s => s.JobId == jobId &&
                            s.UnitNo == targetUnitNo &&
                            s.FinalResult == Core.Enums.SessionResult.Pass)
                .Select(s => s.ProductComponentId)
                .ToListAsync(ct);

            component = job.Product.Components
                .OrderBy(c => c.AssemblyOrder)
                .FirstOrDefault(c => !completedComponentIds.Contains(c.ProductComponentId))
                ?? job.Product.Components.OrderBy(c => c.AssemblyOrder).First();
        }

        // Find default active devices
        var torqueDevice = await _db.Devices
            .Where(d => d.InstrumentType == InstrumentType.Torque && d.IsActive)
            .OrderBy(d => d.DeviceId)
            .FirstOrDefaultAsync(ct);

        var vernierDevice = await _db.Devices
            .Where(d => d.InstrumentType == InstrumentType.Vernier && d.IsActive)
            .OrderBy(d => d.DeviceId)
            .FirstOrDefaultAsync(ct);

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
            CurrentStepSequence = 1
        };

        _db.Sessions.Add(newSession);
        await _db.SaveChangesAsync(ct);

        return newSession;
    }

    public async Task<Session> SwitchComponentAsync(int jobId, int productComponentId, string workerId, CancellationToken ct = default)
    {
        var existingActive = await _db.Sessions
            .FirstOrDefaultAsync(s => s.JobId == jobId &&
                                      s.WorkerId == workerId &&
                                      s.Status == SessionStatus.InProgress, ct);

        if (existingActive != null)
        {
            existingActive.Status = SessionStatus.Abandoned;
            existingActive.EndTime = DateTime.UtcNow;
        }

        return await StartOrResumeSessionAsync(jobId, workerId, productComponentId, ct);
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

    public async Task<ExecutionStateDto> GetExecutionStateAsync(int jobId, string workerId, CancellationToken ct = default)
    {
        var job = await _db.Jobs
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var session = await GetActiveSessionForJobAsync(jobId, workerId, ct)
            ?? await StartOrResumeSessionAsync(jobId, workerId, null, ct);

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

        var templateSteps = currentComponent.Template.Steps.OrderBy(s => s.SequenceNo).ToList();

        // Load all session results for this session
        var sessionResults = await _db.SessionResults
            .Where(r => r.SessionId == session.SessionId)
            .OrderBy(r => r.ReceivedAt)
            .ToListAsync(ct);

        var stepDtos = new List<ExecutionStepDto>();
        foreach (var s in templateSteps)
        {
            var attempts = sessionResults.Where(r => r.StepId == s.TemplateStepId).ToList();
            var hasPassed = attempts.Any(r => r.Result == StepResult.Pass);
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
                IsCompleted: hasPassed,
                IsCurrent: isCurrent,
                AttemptCount: attempts.Count,
                Attempts: attempts
            ));
        }

        var currentStepDto = stepDtos.FirstOrDefault(s => s.IsCurrent)
            ?? stepDtos.FirstOrDefault(s => !s.IsCompleted)
            ?? stepDtos.LastOrDefault();

        var componentDtos = job.Product.Components.OrderBy(c => c.AssemblyOrder).Select(c => new ExecutionComponentDto(
            ProductComponentId: c.ProductComponentId,
            ComponentName: c.ComponentName,
            AssemblyOrder: c.AssemblyOrder,
            Quantity: c.Quantity,
            TemplateId: c.TemplateId,
            TemplateCode: c.Template.TemplateCode,
            TemplateName: c.Template.Name,
            ImagePath: c.Template.ImagePath,
            TemplateMode: c.Template.Mode,
            StepCount: c.Template.Steps.Count,
            IsCompletedForCurrentUnit: completedComponentIds.Contains(c.ProductComponentId),
            IsCurrent: c.ProductComponentId == currentComponent.ProductComponentId
        )).ToList();

        var torqueDev = session.TorqueDeviceId.HasValue
            ? await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == session.TorqueDeviceId.Value, ct)
            : null;

        var vernierDev = session.VernierDeviceId.HasValue
            ? await _db.Devices.FirstOrDefaultAsync(d => d.DeviceId == session.VernierDeviceId.Value, ct)
            : null;

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
            VernierDeviceTransmitterId: vernierDev?.TransmitterId
        );
    }

    public async Task<ReadingEvaluationResult> ProcessReadingAsync(ProcessReadingCommand command, CancellationToken ct = default)
    {
        Session? session = null;

        if (command.SessionId.HasValue)
        {
            session = await _db.Sessions
                .Include(s => s.Job)
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
                .Include(s => s.ProductComponent)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
                .FirstOrDefaultAsync(s => s.Status == SessionStatus.InProgress &&
                                         (s.TorqueDeviceId == command.DeviceId.Value || s.VernierDeviceId == command.DeviceId.Value), ct);
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

        // Deduplication check if DeviceId and DeviceSeq are provided
        if (command.DeviceId.HasValue && command.DeviceSeq.HasValue)
        {
            var dup = await _db.SessionResults
                .Include(r => r.Step)
                .FirstOrDefaultAsync(r => r.DeviceId == command.DeviceId.Value && r.DeviceSeq == command.DeviceSeq.Value, ct);

            if (dup != null)
            {
                return new ReadingEvaluationResult(
                    Success: true,
                    ErrorMessage: null,
                    ResultId: dup.ResultId,
                    SessionId: dup.SessionId,
                    StepId: dup.StepId,
                    StepSequence: dup.Step.SequenceNo,
                    StepLabel: dup.Step.Label,
                    ActualValue: dup.ActualValue,
                    TargetValue: dup.TargetValue,
                    LowerLimit: dup.LowerLimit,
                    UpperLimit: dup.UpperLimit,
                    Result: dup.Result,
                    AttemptNo: dup.AttemptNo,
                    MaxRetries: dup.Step.MaxRetries,
                    IsExceededRetries: false,
                    IsStepPassed: dup.Result == StepResult.Pass,
                    IsSessionCompleted: session.Status == SessionStatus.Completed,
                    IsUnitCompleted: false,
                    IsJobCompleted: session.Job.Status == JobStatus.Completed,
                    NextStepSequence: session.CurrentStepSequence,
                    QuantityCompleted: session.Job.QuantityCompleted,
                    TotalQuantity: session.Job.Quantity
                );
            }
        }

        var steps = session.ProductComponent.Template.Steps.OrderBy(s => s.SequenceNo).ToList();
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
        bool isPass = _evaluator.IsInBand(command.Value, currentStep.Target, currentStep.ToleranceMinus, currentStep.TolerancePlus, currentStep.ToleranceMode);
        var stepResult = isPass ? StepResult.Pass : StepResult.Fail;

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
            ReasonCode = command.ReasonCode,
            DeviceSeq = command.DeviceSeq,
            CapturedAt = command.CapturedAt ?? DateTime.UtcNow,
            ReceivedAt = DateTime.UtcNow
        };

        _db.SessionResults.Add(resultRecord);

        bool isSessionCompleted = false;
        bool isUnitCompleted = false;
        bool isJobCompleted = false;
        int? nextStepSequence = session.CurrentStepSequence;
        bool isExceededRetries = !isPass && attemptNo >= currentStep.MaxRetries;

        if (isPass)
        {
            var nextStep = steps.FirstOrDefault(s => s.SequenceNo > currentStep.SequenceNo);
            if (nextStep != null)
            {
                session.CurrentStepSequence = nextStep.SequenceNo;
                nextStepSequence = nextStep.SequenceNo;
            }
            else
            {
                // All steps passed for this component!
                session.Status = SessionStatus.Completed;
                session.FinalResult = Core.Enums.SessionResult.Pass;
                session.EndTime = DateTime.UtcNow;
                isSessionCompleted = true;

                // Check if all components for this unit are complete
                var totalComponentsCount = await _db.ProductComponents
                    .CountAsync(pc => pc.ProductId == session.Job.ProductId, ct);

                var completedComponentsCount = await _db.Sessions
                    .Where(s => s.JobId == session.JobId &&
                                s.UnitNo == session.UnitNo &&
                                s.FinalResult == Core.Enums.SessionResult.Pass &&
                                s.SessionId != session.SessionId)
                    .Select(s => s.ProductComponentId)
                    .Distinct()
                    .CountAsync(ct) + 1; // including current session

                if (completedComponentsCount >= totalComponentsCount)
                {
                    isUnitCompleted = true;
                    session.Job.QuantityCompleted++;

                    if (session.Job.QuantityCompleted >= session.Job.Quantity)
                    {
                        session.Job.Status = JobStatus.Completed;
                        session.Job.UpdatedAt = DateTime.UtcNow;
                        isJobCompleted = true;
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
}