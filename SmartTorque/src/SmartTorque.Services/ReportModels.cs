using SmartTorque.Core.Enums;

namespace SmartTorque.Services;

public record SessionReportFilter(
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? WorkerId = null,
    int? ProductId = null,
    int? DeviceId = null,
    SmartTorque.Core.Enums.SessionResult? Result = null,
    SessionStatus? Status = null,
    string? Search = null,
    int? DepartmentId = null
);

public record SessionReportItemDto(
    long SessionId,
    int JobId,
    string JobNo,
    string? ProductSerial,
    string ProductCode,
    string ProductName,
    string ComponentName,
    int TemplateVersion,
    string WorkerId,
    string WorkerName,
    string? EmployeeNo,
    int? TorqueDeviceId,
    string? TorqueTransmitterId,
    int? VernierDeviceId,
    string? VernierTransmitterId,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan? Duration,
    SessionStatus Status,
    SmartTorque.Core.Enums.SessionResult FinalResult,
    int ReworkCount,
    int TotalSteps,
    int PassedSteps,
    int FailedSteps,
    string? Remarks,
    int UnitNo = 1,
    string? UnitTokenNo = null
);

public record SessionDetailDto(
    long SessionId,
    int JobId,
    string JobNo,
    string? ProductSerial,
    string ProductCode,
    string ProductName,
    string ComponentName,
    int TemplateVersion,
    string WorkerId,
    string WorkerName,
    string? EmployeeNo,
    int? TorqueDeviceId,
    string? TorqueTransmitterId,
    string? TorqueDeviceName,
    int? VernierDeviceId,
    string? VernierTransmitterId,
    string? VernierDeviceName,
    DateTime StartTime,
    DateTime? EndTime,
    TimeSpan? Duration,
    SessionStatus Status,
    SmartTorque.Core.Enums.SessionResult FinalResult,
    int ReworkCount,
    string? Remarks,
    List<SessionStepResultDto> Steps,
    int UnitNo = 1,
    string? UnitTokenNo = null
);

public record SessionStepResultDto(
    long ResultId,
    int StepId,
    int StepSequence,
    string StepLabel,
    InstrumentType Instrument,
    int? DeviceId,
    string? DeviceName,
    string? TransmitterId,
    int AttemptNo,
    decimal TargetValue,
    decimal TolerancePlus,
    decimal ToleranceMinus,
    ToleranceMode ToleranceMode,
    decimal LowerLimit,
    decimal UpperLimit,
    decimal ActualValue,
    StepResult Result,
    bool IsManualEntry,
    string? ManualApprovedBy,
    string? ReasonCode,
    long? DeviceSeq,
    DateTime CapturedAt,
    DateTime ReceivedAt
);

public record TraceabilityReportDto(
    string Query,
    List<TraceabilityJobDto> Jobs,
    List<TraceabilityDeviceReadingDto> DeviceReadings
);

public record TraceabilityJobDto(
    int JobId,
    string JobNo,
    string? ProductSerial,
    string ProductCode,
    string ProductName,
    JobStatus Status,
    DateTime CreatedAt,
    List<SessionReportItemDto> Sessions
);

public record TraceabilityDeviceReadingDto(
    long ResultId,
    long SessionId,
    string JobNo,
    string? ProductSerial,
    string ComponentName,
    int StepSequence,
    string StepLabel,
    string WorkerName,
    decimal TargetValue,
    decimal LowerLimit,
    decimal UpperLimit,
    decimal ActualValue,
    StepResult Result,
    long? DeviceSeq,
    DateTime CapturedAt
);

public record TemplateSummaryItemDto(
    int TemplateId,
    string TemplateName,
    string TemplateCode,
    int Version,
    int TotalSessions,
    int PassedSessions,
    int FailedSessions,
    decimal PassRate,
    decimal FirstPassYield
);

public record StepFailureSummaryItemDto(
    int StepId,
    string TemplateName,
    int StepSequence,
    string StepLabel,
    InstrumentType Instrument,
    int TotalAttempts,
    int PassedAttempts,
    int FailedAttempts,
    decimal FailureRate
);

public record PassFailSummaryDto(
    int TotalSessions,
    int PassedSessions,
    int FailedSessions,
    int IncompleteSessions,
    decimal OverallPassRate,
    decimal FirstPassYield,
    List<TemplateSummaryItemDto> TemplateSummaries,
    List<StepFailureSummaryItemDto> StepSummaries
);

public record WorkerPerformanceDto(
    string WorkerId,
    string WorkerName,
    string? EmployeeNo,
    int TotalSessions,
    int CompletedSessions,
    int PassedSessions,
    int FailedSessions,
    double AverageDurationMinutes,
    int TotalReworkCount,
    decimal PassRate,
    decimal FirstPassRate
);

public record DeviceUsageDto(
    int DeviceId,
    string TransmitterId,
    string Name,
    InstrumentType InstrumentType,
    string? Location,
    string? FirmwareVersion,
    bool IsActive,
    int TotalSessions,
    int TotalReadings,
    DateTime? LastSeenAt,
    bool IsOnline,
    DateOnly? CalibrationDue,
    string CalibrationStatus
);

public record RecentSessionDto(
    long SessionId,
    string JobNo,
    string? ProductSerial,
    string ProductName,
    string ComponentName,
    string WorkerName,
    DateTime StartTime,
    TimeSpan? Duration,
    SmartTorque.Core.Enums.SessionResult FinalResult
);

public record DashboardStatsDto(
    Dictionary<JobStatus, int> JobsByStatus,
    int TotalJobsToday,
    decimal TodayPassRate,
    decimal TodayFirstPassYield,
    int TodaySessionsCount,
    int TotalDevicesCount,
    int OnlineDevicesCount,
    int OnlineTorqueCount,
    int OnlineVernierCount,
    int CriticalBlackJobsWaiting,
    List<RecentSessionDto> RecentSessions
);