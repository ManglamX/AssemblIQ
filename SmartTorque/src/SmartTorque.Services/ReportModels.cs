using SmartTorque.Core.Enums;
using SmartTorque.Core.Extensions;

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
    int? DepartmentId = null,
    int? AssemblyLineId = null,
    int? WorkStationId = null
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
    List<TraceabilityDeviceReadingDto> DeviceReadings,
    List<JobUnitReworkDto>? ReworkHistory = null
);

public class JobUnitReworkDto
{
    public int JobUnitReworkId { get; set; }
    public int JobUnitId { get; set; }
    public int JobId { get; set; }
    public int UnitIndex { get; set; }
    public string TokenNo { get; set; } = string.Empty;
    public int ReworkIteration { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string? DefectCategory { get; set; }
    public string? TargetTemplateIds { get; set; }
    public string InitiatedByUserId { get; set; } = string.Empty;
    public string InitiatedByUserName { get; set; } = string.Empty;
    public DateTime InitiatedAt { get; set; }
    public string InitiatedAtIst => InitiatedAt.ToIstString("dd MMM yyyy, HH:mm");
    public UnitAssemblyStatus Status { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedAtIst => CompletedAt.HasValue ? CompletedAt.Value.ToIstString("dd MMM yyyy, HH:mm") : null;
    public string? CompletedByUserId { get; set; }
    public string? CompletedByUserName { get; set; }
    public string? ResolutionNotes { get; set; }
    public SmartTorque.Core.Enums.SessionResult? FinalResult { get; set; }
}

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
    DateTime CapturedAt,
    int AttemptNo = 1,
    int SessionReworkCount = 0
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

public class WorkerPerformanceFilter
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public int? DepartmentId { get; set; }
    public int? AssemblyLineId { get; set; }
    public int? WorkStationId { get; set; }
    public int? ProductId { get; set; }
    public string? WorkerId { get; set; }
    public string? SortBy { get; set; } = "CompletedSessions";
    public string? SortOrder { get; set; } = "desc";
    public string ViewMode { get; set; } = "workers";
}

public class WorkerStationDetailDto
{
    public int? WorkStationId { get; set; }
    public string StationName { get; set; } = "General / Dept Mode";
    public string StationCode { get; set; } = "DEPT";
    public string StationType { get; set; } = "Department";
    public int? AssemblyLineId { get; set; }
    public string LineName { get; set; } = "Engg Cell";
    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "Engg Cell";
    public int ProductId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MappedBoltsSummary { get; set; } = "Full Product";
    public int CompletedSessions { get; set; }
    public int PassedSessions { get; set; }
    public int FailedSessions { get; set; }
    public double WorkerAvgDurationSeconds { get; set; }
    public double StationBenchmarkDurationSeconds { get; set; }
    public double VariancePercent { get; set; } // positive means worker is faster than benchmark
    public decimal PassRate { get; set; }
    public decimal FirstPassRate { get; set; }
    public int ReworkCount { get; set; }
    public string PerformanceBadge { get; set; } = "Standard";
}

public class StationWorkerSummaryDto
{
    public string WorkerId { get; set; } = string.Empty;
    public string WorkerName { get; set; } = string.Empty;
    public string? EmployeeNo { get; set; }
    public int UnitsCompleted { get; set; }
    public int PassedUnits { get; set; }
    public int FailedUnits { get; set; }
    public double WorkerAvgDurationSeconds { get; set; }
    public double StationAvgDurationSeconds { get; set; }
    public double VariancePercent { get; set; } // positive means faster than station avg
    public decimal PassRate { get; set; }
    public decimal FirstPassRate { get; set; }
    public int ReworkCount { get; set; }
    public bool IsTopPerformer { get; set; }
    public string PaceRating { get; set; } = "Standard";
}

public class StationPerformanceDto
{
    public int? WorkStationId { get; set; }
    public string StationCode { get; set; } = "DEPT";
    public string StationName { get; set; } = "General / Dept Mode";
    public string StationType { get; set; } = "Department";
    public int? AssemblyLineId { get; set; }
    public string LineName { get; set; } = "Engg Cell";
    public string LineType { get; set; } = "Line";
    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = "Engg Cell";
    public int ProductId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string MappedBoltsSummary { get; set; } = "All Fasteners";
    public int TotalUnitsCompleted { get; set; }
    public int PassedUnits { get; set; }
    public int FailedUnits { get; set; }
    public double StationAvgDurationSeconds { get; set; }
    public double MinDurationSeconds { get; set; }
    public double MaxDurationSeconds { get; set; }
    public decimal PassRate { get; set; }
    public decimal FirstPassRate { get; set; }
    public int TotalReworkCount { get; set; }
    public int ActiveWorkerCount { get; set; }
    public List<StationWorkerSummaryDto> Workers { get; set; } = new();
}

public class WorkerPerformanceReportResultDto
{
    public List<WorkerPerformanceDto> Workers { get; set; } = new();
    public List<StationPerformanceDto> Stations { get; set; } = new();
    public int TotalSessionsCount { get; set; }
    public double OverallAvgDurationSeconds { get; set; }
    public decimal OverallPassRate { get; set; }
    public decimal OverallFirstPassRate { get; set; }
    public string? TopWorkerName { get; set; }
    public string? TopStationName { get; set; }
}

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
)
{
    public string? AssignedLineName { get; init; }
    public string? AssignedStationName { get; init; }
    public string? DepartmentName { get; init; }
    public double AverageDurationSeconds { get; init; }
    public double StationBenchmarkSeconds { get; init; }
    public double OverallVariancePercent { get; init; }
    public string PaceStatus { get; init; } = "Standard";
    public List<WorkerStationDetailDto> StationDetails { get; init; } = new();
}

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