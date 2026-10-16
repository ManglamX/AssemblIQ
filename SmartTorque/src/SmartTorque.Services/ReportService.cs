using ClosedXML.Excel;

using Microsoft.EntityFrameworkCore;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

public interface IReportService
{
    Task<List<SessionReportItemDto>> GetSessionReportAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId, CancellationToken ct = default);
    Task<TraceabilityReportDto> GetTraceabilityReportAsync(string query, CancellationToken ct = default);
    Task<PassFailSummaryDto> GetPassFailSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<WorkerPerformanceDto>> GetWorkerPerformanceReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default);
    Task<List<DeviceUsageDto>> GetDeviceUsageReportAsync(CancellationToken ct = default);
    Task<DashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken ct = default);
    Task<byte[]> ExportSessionsToExcelAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportSessionsToPdfAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportTraceabilityToExcelAsync(string query, CancellationToken ct = default);
    Task<byte[]> ExportTraceabilityToPdfAsync(string query, CancellationToken ct = default);
}

public class ReportService : IReportService
{
    private readonly AppDbContext _db;

    static ReportService()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ReportService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<SessionReportItemDto>> GetSessionReportAsync(SessionReportFilter filter, CancellationToken ct = default)
    {
        var query = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.ProductComponent)
                .ThenInclude(pc => pc.Template)
            .Include(s => s.Worker)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Include(s => s.Results)
            .AsQueryable();

        if (filter.FromDate.HasValue)
        {
            query = query.Where(s => s.StartTime >= filter.FromDate.Value);
        }

        if (filter.ToDate.HasValue)
        {
            query = query.Where(s => s.StartTime <= filter.ToDate.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.WorkerId))
        {
            query = query.Where(s => s.WorkerId == filter.WorkerId);
        }

        if (filter.ProductId.HasValue)
        {
            query = query.Where(s => s.Job.ProductId == filter.ProductId.Value);
        }

        if (filter.DeviceId.HasValue)
        {
            query = query.Where(s => s.TorqueDeviceId == filter.DeviceId.Value || s.VernierDeviceId == filter.DeviceId.Value);
        }

        if (filter.Result.HasValue)
        {
            query = query.Where(s => s.FinalResult == filter.Result.Value);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(s => s.Status == filter.Status.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            query = query.Where(s =>
                s.Job.JobNo.ToLower().Contains(search) ||
                (s.Job.ProductSerial != null && s.Job.ProductSerial.ToLower().Contains(search)) ||
                s.Job.Product.ProductCode.ToLower().Contains(search) ||
                s.Job.Product.ProductName.ToLower().Contains(search) ||
                s.Worker.FullName.ToLower().Contains(search) ||
                (s.Worker.EmployeeNo != null && s.Worker.EmployeeNo.ToLower().Contains(search)));
        }

        var sessions = await query
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(ct);

        return sessions.Select(s =>
        {
            var totalSteps = s.Results.Select(r => r.StepId).Distinct().Count();
            var passedSteps = s.Results.Where(r => r.Result == StepResult.Pass).Select(r => r.StepId).Distinct().Count();
            var failedSteps = s.Results.Where(r => r.Result == StepResult.Fail).Select(r => r.StepId).Distinct().Count();

            return new SessionReportItemDto(
                s.SessionId,
                s.JobId,
                s.Job.JobNo,
                s.Job.ProductSerial,
                s.Job.Product.ProductCode,
                s.Job.Product.ProductName,
                s.ProductComponent.ComponentName,
                s.ProductComponent.Template.Version,
                s.WorkerId,
                s.Worker.FullName,
                s.Worker.EmployeeNo,
                s.TorqueDeviceId,
                s.TorqueDevice?.TransmitterId,
                s.VernierDeviceId,
                s.VernierDevice?.TransmitterId,
                s.StartTime,
                s.EndTime,
                s.EndTime.HasValue ? s.EndTime.Value - s.StartTime : null,
                s.Status,
                s.FinalResult,
                s.ReworkCount,
                totalSteps,
                passedSteps,
                failedSteps,
                s.Remarks
            );
        }).ToList();
    }

    public async Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId, CancellationToken ct = default)
    {
        var session = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.ProductComponent)
                .ThenInclude(pc => pc.Template)
            .Include(s => s.Worker)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Include(s => s.Results)
                .ThenInclude(r => r.Step)
            .Include(s => s.Results)
                .ThenInclude(r => r.Device)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId, ct);

        if (session == null)
        {
            return null;
        }

        var stepDtos = session.Results
            .OrderBy(r => r.Step.SequenceNo)
            .ThenBy(r => r.AttemptNo)
            .Select(r => new SessionStepResultDto(
                r.ResultId,
                r.StepId,
                r.Step.SequenceNo,
                r.Step.Label,
                r.InstrumentType,
                r.DeviceId,
                r.Device?.Name,
                r.Device?.TransmitterId,
                r.AttemptNo,
                r.TargetValue,
                r.TolerancePlus,
                r.ToleranceMinus,
                r.ToleranceMode,
                r.LowerLimit,
                r.UpperLimit,
                r.ActualValue,
                r.Result,
                r.IsManualEntry,
                r.ManualApprovedBy,
                r.ReasonCode,
                r.DeviceSeq,
                r.CapturedAt,
                r.ReceivedAt
            ))
            .ToList();

        return new SessionDetailDto(
            session.SessionId,
            session.JobId,
            session.Job.JobNo,
            session.Job.ProductSerial,
            session.Job.Product.ProductCode,
            session.Job.Product.ProductName,
            session.ProductComponent.ComponentName,
            session.ProductComponent.Template.Version,
            session.WorkerId,
            session.Worker.FullName,
            session.Worker.EmployeeNo,
            session.TorqueDeviceId,
            session.TorqueDevice?.TransmitterId,
            session.TorqueDevice?.Name,
            session.VernierDeviceId,
            session.VernierDevice?.TransmitterId,
            session.VernierDevice?.Name,
            session.StartTime,
            session.EndTime,
            session.EndTime.HasValue ? session.EndTime.Value - session.StartTime : null,
            session.Status,
            session.FinalResult,
            session.ReworkCount,
            session.Remarks,
            stepDtos
        );
    }

    public async Task<TraceabilityReportDto> GetTraceabilityReportAsync(string query, CancellationToken ct = default)
    {
        var cleanQuery = (query ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cleanQuery))
        {
            return new TraceabilityReportDto(cleanQuery, new List<TraceabilityJobDto>(), new List<TraceabilityDeviceReadingDto>());
        }

        var lowerQuery = cleanQuery.ToLower();

        // 1. Search for matching jobs by ProductSerial or JobNo
        var matchingJobs = await _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
            .Where(j => (j.ProductSerial != null && j.ProductSerial.ToLower().Contains(lowerQuery)) ||
                        j.JobNo.ToLower().Contains(lowerQuery))
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var jobDtos = new List<TraceabilityJobDto>();
        foreach (var job in matchingJobs)
        {
            var sessions = await GetSessionReportAsync(new SessionReportFilter(Search: job.JobNo), ct);
            jobDtos.Add(new TraceabilityJobDto(
                job.JobId,
                job.JobNo,
                job.ProductSerial,
                job.Product.ProductCode,
                job.Product.ProductName,
                job.Status,
                job.CreatedAt,
                sessions
            ));
        }

        // 2. Search for readings by Device TransmitterId or Name
        var deviceReadings = await _db.SessionResults
            .AsNoTracking()
            .Include(r => r.Device)
            .Include(r => r.Step)
            .Include(r => r.Session)
                .ThenInclude(s => s.Job)
            .Include(r => r.Session)
                .ThenInclude(s => s.ProductComponent)
            .Include(r => r.Session)
                .ThenInclude(s => s.Worker)
            .Where(r => r.Device != null &&
                        (r.Device.TransmitterId.ToLower().Contains(lowerQuery) ||
                         r.Device.Name.ToLower().Contains(lowerQuery)))
            .OrderByDescending(r => r.CapturedAt)
            .Take(100)
            .Select(r => new TraceabilityDeviceReadingDto(
                r.ResultId,
                r.SessionId,
                r.Session.Job.JobNo,
                r.Session.Job.ProductSerial,
                r.Session.ProductComponent.ComponentName,
                r.Step.SequenceNo,
                r.Step.Label,
                r.Session.Worker.FullName,
                r.TargetValue,
                r.LowerLimit,
                r.UpperLimit,
                r.ActualValue,
                r.Result,
                r.DeviceSeq,
                r.CapturedAt
            ))
            .ToListAsync(ct);

        return new TraceabilityReportDto(cleanQuery, jobDtos, deviceReadings);
    }

    public async Task<PassFailSummaryDto> GetPassFailSummaryAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var sessionQuery = _db.Sessions
            .AsNoTracking()
            .Include(s => s.ProductComponent)
                .ThenInclude(pc => pc.Template)
            .Include(s => s.Results)
            .AsQueryable();

        if (from.HasValue)
        {
            sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            sessionQuery = sessionQuery.Where(s => s.StartTime <= to.Value);
        }

        var sessions = await sessionQuery.ToListAsync(ct);

        var totalSessions = sessions.Count;
        var passedSessions = sessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass);
        var failedSessions = sessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail);
        var incompleteSessions = sessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Incomplete ||
                                                     s.Status == SessionStatus.Abandoned);

        var completedCount = passedSessions + failedSessions;
        var overallPassRate = completedCount > 0
            ? Math.Round((decimal)passedSessions / completedCount * 100m, 2)
            : 0m;

        // First-Pass Yield (FPY): completed sessions with Pass result AND 0 rework AND 0 failed step attempts
        var firstPassSessions = sessions.Count(s =>
            s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass &&
            s.ReworkCount == 0 &&
            !s.Results.Any(r => r.Result == StepResult.Fail));

        var fpy = completedCount > 0
            ? Math.Round((decimal)firstPassSessions / completedCount * 100m, 2)
            : 0m;

        // Breakdown by Template
        var templateSummaries = sessions
            .GroupBy(s => new
            {
                s.ProductComponent.Template.TemplateId,
                s.ProductComponent.Template.Name,
                s.ProductComponent.Template.TemplateCode,
                s.ProductComponent.Template.Version
            })
            .Select(g =>
            {
                var tTotal = g.Count();
                var tPassed = g.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass);
                var tFailed = g.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail);
                var tCompleted = tPassed + tFailed;
                var tPassRate = tCompleted > 0 ? Math.Round((decimal)tPassed / tCompleted * 100m, 2) : 0m;

                var tFpyCount = g.Count(s =>
                    s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass &&
                    s.ReworkCount == 0 &&
                    !s.Results.Any(r => r.Result == StepResult.Fail));
                var tFpy = tCompleted > 0 ? Math.Round((decimal)tFpyCount / tCompleted * 100m, 2) : 0m;

                return new TemplateSummaryItemDto(
                    g.Key.TemplateId,
                    g.Key.Name,
                    g.Key.TemplateCode,
                    g.Key.Version,
                    tTotal,
                    tPassed,
                    tFailed,
                    tPassRate,
                    tFpy
                );
            })
            .OrderByDescending(t => t.TotalSessions)
            .ToList();

        // Breakdown by Step / Bolt
        var resultQuery = _db.SessionResults
            .AsNoTracking()
            .Include(r => r.Step)
                .ThenInclude(st => st.Template)
            .AsQueryable();

        if (from.HasValue)
        {
            resultQuery = resultQuery.Where(r => r.CapturedAt >= from.Value);
        }

        if (to.HasValue)
        {
            resultQuery = resultQuery.Where(r => r.CapturedAt <= to.Value);
        }

        var results = await resultQuery.ToListAsync(ct);

        var stepSummaries = results
            .GroupBy(r => new
            {
                r.StepId,
                TemplateName = r.Step.Template != null ? r.Step.Template.Name : "Unknown Template",
                r.Step.SequenceNo,
                r.Step.Label,
                r.Step.Instrument
            })
            .Select(g =>
            {
                var totalAttempts = g.Count();
                var passedAttempts = g.Count(r => r.Result == StepResult.Pass);
                var failedAttempts = g.Count(r => r.Result == StepResult.Fail);
                var failureRate = totalAttempts > 0
                    ? Math.Round((decimal)failedAttempts / totalAttempts * 100m, 2)
                    : 0m;

                return new StepFailureSummaryItemDto(
                    g.Key.StepId,
                    g.Key.TemplateName,
                    g.Key.SequenceNo,
                    g.Key.Label,
                    g.Key.Instrument,
                    totalAttempts,
                    passedAttempts,
                    failedAttempts,
                    failureRate
                );
            })
            .OrderByDescending(s => s.FailureRate)
            .ThenByDescending(s => s.FailedAttempts)
            .Take(50)
            .ToList();

        return new PassFailSummaryDto(
            totalSessions,
            passedSessions,
            failedSessions,
            incompleteSessions,
            overallPassRate,
            fpy,
            templateSummaries,
            stepSummaries
        );
    }

    public async Task<List<WorkerPerformanceDto>> GetWorkerPerformanceReportAsync(DateTime? from, DateTime? to, CancellationToken ct = default)
    {
        var sessionQuery = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Worker)
            .Include(s => s.Results)
            .AsQueryable();

        if (from.HasValue)
        {
            sessionQuery = sessionQuery.Where(s => s.StartTime >= from.Value);
        }

        if (to.HasValue)
        {
            sessionQuery = sessionQuery.Where(s => s.StartTime <= to.Value);
        }

        var sessions = await sessionQuery.ToListAsync(ct);

        var workerList = sessions
            .GroupBy(s => new { s.WorkerId, s.Worker.FullName, s.Worker.EmployeeNo })
            .Select(g =>
            {
                var total = g.Count();
                var completed = g.Count(s => s.EndTime.HasValue);
                var passed = g.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass);
                var failed = g.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail);
                var totalRework = g.Sum(s => s.ReworkCount);

                var completedWithDuration = g.Where(s => s.EndTime.HasValue).ToList();
                var avgDurationMinutes = completedWithDuration.Count > 0
                    ? Math.Round(completedWithDuration.Average(s => (s.EndTime!.Value - s.StartTime).TotalMinutes), 1)
                    : 0;

                var passRate = completed > 0 ? Math.Round((decimal)passed / completed * 100m, 2) : 0m;

                var firstPassCount = g.Count(s =>
                    s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass &&
                    s.ReworkCount == 0 &&
                    !s.Results.Any(r => r.Result == StepResult.Fail));
                var firstPassRate = completed > 0 ? Math.Round((decimal)firstPassCount / completed * 100m, 2) : 0m;

                return new WorkerPerformanceDto(
                    g.Key.WorkerId,
                    g.Key.FullName,
                    g.Key.EmployeeNo,
                    total,
                    completed,
                    passed,
                    failed,
                    avgDurationMinutes,
                    totalRework,
                    passRate,
                    firstPassRate
                );
            })
            .OrderByDescending(w => w.CompletedSessions)
            .ToList();

        return workerList;
    }

    public async Task<List<DeviceUsageDto>> GetDeviceUsageReportAsync(CancellationToken ct = default)
    {
        var devices = await _db.Devices
            .AsNoTracking()
            .Include(d => d.TorqueSessions)
            .Include(d => d.VernierSessions)
            .Include(d => d.SessionResults)
            .OrderBy(d => d.InstrumentType)
            .ThenBy(d => d.Name)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var onlineThreshold = now.AddSeconds(-30);

        return devices.Select(d =>
        {
            var isOnline = d.LastSeenAt.HasValue && d.LastSeenAt.Value >= onlineThreshold;
            var totalSessions = (d.TorqueSessions?.Count ?? 0) + (d.VernierSessions?.Count ?? 0);
            var totalReadings = d.SessionResults?.Count ?? 0;

            string calibrationStatus;
            if (!d.CalibrationDue.HasValue)
            {
                calibrationStatus = "Not Set";
            }
            else if (d.CalibrationDue.Value < today)
            {
                calibrationStatus = "Overdue";
            }
            else if (d.CalibrationDue.Value <= today.AddDays(30))
            {
                calibrationStatus = "Due Soon";
            }
            else
            {
                calibrationStatus = "Current";
            }

            return new DeviceUsageDto(
                d.DeviceId,
                d.TransmitterId,
                d.Name,
                d.InstrumentType,
                d.Location,
                d.FirmwareVersion,
                d.IsActive,
                totalSessions,
                totalReadings,
                d.LastSeenAt,
                isOnline,
                d.CalibrationDue,
                calibrationStatus
            );
        }).ToList();
    }

    public async Task<DashboardStatsDto> GetAdminDashboardStatsAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var onlineThreshold = now.AddSeconds(-30);

        // 1. Jobs by status
        var jobs = await _db.Jobs.AsNoTracking().ToListAsync(ct);
        var jobsByStatus = Enum.GetValues<JobStatus>()
            .ToDictionary(status => status, status => jobs.Count(j => j.Status == status));

        var totalJobsToday = jobs.Count(j => j.CreatedAt >= todayStart);

        // 2. Critical Black jobs waiting
        var criticalBlackJobs = jobs.Count(j =>
            j.Priority == PriorityLevel.Black &&
            (j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned));

        // 3. Devices online
        var devices = await _db.Devices.AsNoTracking().ToListAsync(ct);
        var totalDevices = devices.Count;
        var onlineDevices = devices.Where(d => d.LastSeenAt.HasValue && d.LastSeenAt.Value >= onlineThreshold).ToList();
        var onlineDevicesCount = onlineDevices.Count;
        var onlineTorqueCount = onlineDevices.Count(d => d.InstrumentType == InstrumentType.Torque);
        var onlineVernierCount = onlineDevices.Count(d => d.InstrumentType == InstrumentType.Vernier);

        // 4. Today's sessions & pass rate
        var todaySessions = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Results)
            .Where(s => s.StartTime >= todayStart)
            .ToListAsync(ct);

        var todaySessionsCount = todaySessions.Count;
        var todayPassed = todaySessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass);
        var todayCompleted = todaySessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass ||
                                                      s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail);

        var todayPassRate = todayCompleted > 0
            ? Math.Round((decimal)todayPassed / todayCompleted * 100m, 1)
            : 0m;

        var todayFirstPass = todaySessions.Count(s =>
            s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass &&
            s.ReworkCount == 0 &&
            !s.Results.Any(r => r.Result == StepResult.Fail));

        var todayFpy = todayCompleted > 0
            ? Math.Round((decimal)todayFirstPass / todayCompleted * 100m, 1)
            : 0m;

        // 5. Recent sessions (top 8)
        var recentSessionsData = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.ProductComponent)
            .Include(s => s.Worker)
            .OrderByDescending(s => s.StartTime)
            .Take(8)
            .ToListAsync(ct);

        var recentSessions = recentSessionsData.Select(s => new RecentSessionDto(
            s.SessionId,
            s.Job.JobNo,
            s.Job.ProductSerial,
            s.Job.Product.ProductName,
            s.ProductComponent.ComponentName,
            s.Worker.FullName,
            s.StartTime,
            s.EndTime.HasValue ? s.EndTime.Value - s.StartTime : null,
            s.FinalResult
        )).ToList();

        return new DashboardStatsDto(
            jobsByStatus,
            totalJobsToday,
            todayPassRate,
            todayFpy,
            todaySessionsCount,
            totalDevices,
            onlineDevicesCount,
            onlineTorqueCount,
            onlineVernierCount,
            criticalBlackJobs,
            recentSessions
        );
    }

    public async Task<byte[]> ExportSessionsToExcelAsync(SessionReportFilter filter, CancellationToken ct = default)
    {
        var sessions = await GetSessionReportAsync(filter, ct);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Session Report");

        // Title row
        ws.Cell(1, 1).Value = "SmartTorque — Quality Assembly Traceability Session Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 13).Merge();

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Total Sessions: {sessions.Count}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontSize = 10;
        ws.Range(2, 1, 2, 13).Merge();

        // Table headers
        var headers = new[]
        {
            "Session ID", "Job No", "Product Serial", "Product", "Component", "Version",
            "Worker", "Torque Device", "Vernier Device", "Start Time (UTC)", "Duration (min)",
            "Status", "Final Result", "Rework Count", "Steps (Total/Pass/Fail)"
        };

        for (var i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(4, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        var row = 5;
        foreach (var s in sessions)
        {
            ws.Cell(row, 1).Value = s.SessionId;
            ws.Cell(row, 2).Value = s.JobNo;
            ws.Cell(row, 3).Value = s.ProductSerial ?? "—";
            ws.Cell(row, 4).Value = $"{s.ProductCode} ({s.ProductName})";
            ws.Cell(row, 5).Value = s.ComponentName;
            ws.Cell(row, 6).Value = $"v{s.TemplateVersion}";
            ws.Cell(row, 7).Value = $"{s.WorkerName} ({s.EmployeeNo ?? "N/A"})";
            ws.Cell(row, 8).Value = s.TorqueTransmitterId ?? "—";
            ws.Cell(row, 9).Value = s.VernierTransmitterId ?? "—";
            ws.Cell(row, 10).Value = s.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
            ws.Cell(row, 11).Value = s.Duration.HasValue ? Math.Round(s.Duration.Value.TotalMinutes, 1).ToString("0.0") : "—";
            ws.Cell(row, 12).Value = s.Status.ToString();

            var resultCell = ws.Cell(row, 13);
            resultCell.Value = s.FinalResult.ToString();
            if (s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass)
            {
                resultCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#DCFCE7");
                resultCell.Style.Font.FontColor = XLColor.FromHtml("#166534");
            }
            else if (s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail)
            {
                resultCell.Style.Fill.BackgroundColor = XLColor.FromHtml("#FEE2E2");
                resultCell.Style.Font.FontColor = XLColor.FromHtml("#991B1B");
            }

            ws.Cell(row, 14).Value = s.ReworkCount;
            ws.Cell(row, 15).Value = $"{s.TotalSteps} / {s.PassedSteps} / {s.FailedSteps}";

            row++;
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportSessionsToPdfAsync(SessionReportFilter filter, CancellationToken ct = default)
    {
        var sessions = await GetSessionReportAsync(filter, ct);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Row(r =>
                    {
                        r.RelativeItem().Column(c =>
                        {
                            c.Item().Text("SmartTorque Assembly Traceability Report").Bold().FontSize(16).FontColor("#0F172A");
                            c.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC | Total records: {sessions.Count}").FontSize(9).FontColor("#64748B");
                        });
                    });
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().PaddingVertical(5).Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(45);  // Session
                        columns.RelativeColumn(1.2f); // Job No
                        columns.RelativeColumn(1.2f); // Serial
                        columns.RelativeColumn(1.4f); // Product
                        columns.RelativeColumn(1.2f); // Component
                        columns.RelativeColumn(1.2f); // Worker
                        columns.RelativeColumn(1.2f); // Devices
                        columns.ConstantColumn(85);  // Start
                        columns.ConstantColumn(45);  // Duration
                        columns.ConstantColumn(50);  // Result
                    });

                    // Headers
                    table.Header(header =>
                    {
                        header.Cell().Background("#1E293B").Padding(4).Text("ID").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Job No").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Serial").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Product").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Component").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Worker").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Devices").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Start Time").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Dur (m)").Bold().FontColor("#FFFFFF");
                        header.Cell().Background("#1E293B").Padding(4).Text("Result").Bold().FontColor("#FFFFFF");
                    });

                    var idx = 0;
                    foreach (var s in sessions)
                    {
                        var bg = (idx++ % 2 == 0) ? "#FFFFFF" : "#F8FAFC";
                        var resultColor = s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass ? "#166534" :
                                          s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail ? "#991B1B" : "#475569";

                        table.Cell().Background(bg).Padding(4).Text($"#{s.SessionId}");
                        table.Cell().Background(bg).Padding(4).Text(s.JobNo);
                        table.Cell().Background(bg).Padding(4).Text(s.ProductSerial ?? "—");
                        table.Cell().Background(bg).Padding(4).Text(s.ProductCode);
                        table.Cell().Background(bg).Padding(4).Text(s.ComponentName);
                        table.Cell().Background(bg).Padding(4).Text(s.WorkerName);
                        table.Cell().Background(bg).Padding(4).Text($"{s.TorqueTransmitterId ?? "—"} / {s.VernierTransmitterId ?? "—"}");
                        table.Cell().Background(bg).Padding(4).Text(s.StartTime.ToString("yyyy-MM-dd HH:mm"));
                        table.Cell().Background(bg).Padding(4).Text(s.Duration.HasValue ? $"{s.Duration.Value.TotalMinutes:0.0}" : "—");
                        table.Cell().Background(bg).Padding(4).Text(s.FinalResult.ToString()).Bold().FontColor(resultColor);
                    }
                });

                page.Footer().Column(c =>
                {
                    c.Item().LineHorizontal(1).LineColor("#CBD5E1");
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("SmartTorque Plant Quality System").FontSize(8).FontColor("#94A3B8");
                        r.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Page ");
                            x.CurrentPageNumber();
                            x.Span(" of ");
                            x.TotalPages();
                        });
                    });
                });
            });
        });

        return document.GeneratePdf();
    }

    public async Task<byte[]> ExportTraceabilityToExcelAsync(string query, CancellationToken ct = default)
    {
        var trace = await GetTraceabilityReportAsync(query, ct);

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Traceability");

        ws.Cell(1, 1).Value = $"SmartTorque — Traceability Audit Report: Query '{trace.Query}'";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;

        var row = 3;

        if (trace.Jobs.Count > 0)
        {
            ws.Cell(row, 1).Value = "Matching Jobs & Sessions";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row++;

            var jobHeaders = new[] { "Job No", "Product Serial", "Product", "Component", "Worker", "Start Time", "Result" };
            for (var i = 0; i < jobHeaders.Length; i++)
            {
                var c = ws.Cell(row, i + 1);
                c.Value = jobHeaders[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                c.Style.Font.FontColor = XLColor.White;
            }
            row++;

            foreach (var j in trace.Jobs)
            {
                foreach (var s in j.Sessions)
                {
                    ws.Cell(row, 1).Value = j.JobNo;
                    ws.Cell(row, 2).Value = j.ProductSerial ?? "—";
                    ws.Cell(row, 3).Value = $"{j.ProductCode} ({j.ProductName})";
                    ws.Cell(row, 4).Value = s.ComponentName;
                    ws.Cell(row, 5).Value = s.WorkerName;
                    ws.Cell(row, 6).Value = s.StartTime.ToString("yyyy-MM-dd HH:mm:ss");
                    ws.Cell(row, 7).Value = s.FinalResult.ToString();
                    row++;
                }
            }
            row += 2;
        }

        if (trace.DeviceReadings.Count > 0)
        {
            ws.Cell(row, 1).Value = "Device Readings Timeline";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 12;
            row++;

            var readingHeaders = new[] { "Reading ID", "Job No", "Component", "Step", "Worker", "Target", "Limits", "Actual", "Result", "Captured At" };
            for (var i = 0; i < readingHeaders.Length; i++)
            {
                var c = ws.Cell(row, i + 1);
                c.Value = readingHeaders[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E293B");
                c.Style.Font.FontColor = XLColor.White;
            }
            row++;

            foreach (var r in trace.DeviceReadings)
            {
                ws.Cell(row, 1).Value = r.ResultId;
                ws.Cell(row, 2).Value = r.JobNo;
                ws.Cell(row, 3).Value = r.ComponentName;
                ws.Cell(row, 4).Value = $"#{r.StepSequence} {r.StepLabel}";
                ws.Cell(row, 5).Value = r.WorkerName;
                ws.Cell(row, 6).Value = r.TargetValue;
                ws.Cell(row, 7).Value = $"{r.LowerLimit} .. {r.UpperLimit}";
                ws.Cell(row, 8).Value = r.ActualValue;
                ws.Cell(row, 9).Value = r.Result.ToString();
                ws.Cell(row, 10).Value = r.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss");
                row++;
            }
        }

        ws.Columns().AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    public async Task<byte[]> ExportTraceabilityToPdfAsync(string query, CancellationToken ct = default)
    {
        var trace = await GetTraceabilityReportAsync(query, ct);

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text($"SmartTorque — Traceability Audit: '{trace.Query}'").Bold().FontSize(16).FontColor("#0F172A");
                    col.Item().Text($"Generated: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC").FontSize(9).FontColor("#64748B");
                    col.Item().PaddingVertical(5).LineHorizontal(1).LineColor("#CBD5E1");
                });

                page.Content().Column(content =>
                {
                    if (trace.Jobs.Count > 0)
                    {
                        content.Item().Text("Matching Jobs & Assembly Sessions").Bold().FontSize(12).FontColor("#1E293B");
                        content.Item().PaddingVertical(4).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.5f);
                                c.ConstantColumn(60);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Background("#1E293B").Padding(3).Text("Job No").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Serial").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Product").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Component").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Worker").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Time").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Result").Bold().FontColor("#FFFFFF");
                            });

                            foreach (var j in trace.Jobs)
                            {
                                foreach (var s in j.Sessions)
                                {
                                    t.Cell().Padding(3).Text(j.JobNo);
                                    t.Cell().Padding(3).Text(j.ProductSerial ?? "—");
                                    t.Cell().Padding(3).Text(j.ProductCode);
                                    t.Cell().Padding(3).Text(s.ComponentName);
                                    t.Cell().Padding(3).Text(s.WorkerName);
                                    t.Cell().Padding(3).Text(s.StartTime.ToString("yyyy-MM-dd HH:mm"));
                                    t.Cell().Padding(3).Text(s.FinalResult.ToString()).Bold();
                                }
                            }
                        });
                    }

                    if (trace.DeviceReadings.Count > 0)
                    {
                        content.Item().PaddingTop(10).Text("Device Readings Log").Bold().FontSize(12).FontColor("#1E293B");
                        content.Item().PaddingVertical(4).Table(t =>
                        {
                            t.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(50);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.2f);
                                c.RelativeColumn(1.5f);
                                c.RelativeColumn(1.2f);
                                c.ConstantColumn(50);
                                c.ConstantColumn(70);
                                c.ConstantColumn(50);
                                c.ConstantColumn(50);
                                c.RelativeColumn(1.4f);
                            });

                            t.Header(h =>
                            {
                                h.Cell().Background("#1E293B").Padding(3).Text("ID").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Job No").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Component").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Step").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Worker").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Target").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Limits").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Actual").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Result").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Padding(3).Text("Captured At").Bold().FontColor("#FFFFFF");
                            });

                            foreach (var r in trace.DeviceReadings)
                            {
                                t.Cell().Padding(3).Text($"#{r.ResultId}");
                                t.Cell().Padding(3).Text(r.JobNo);
                                t.Cell().Padding(3).Text(r.ComponentName);
                                t.Cell().Padding(3).Text($"#{r.StepSequence} {r.StepLabel}");
                                t.Cell().Padding(3).Text(r.WorkerName);
                                t.Cell().Padding(3).Text($"{r.TargetValue:0.00}");
                                t.Cell().Padding(3).Text($"{r.LowerLimit:0.00}..{r.UpperLimit:0.00}");
                                t.Cell().Padding(3).Text($"{r.ActualValue:0.00}");
                                t.Cell().Padding(3).Text(r.Result.ToString()).Bold();
                                t.Cell().Padding(3).Text(r.CapturedAt.ToString("yyyy-MM-dd HH:mm:ss"));
                            }
                        });
                    }
                });

                page.Footer().AlignRight().Text(x =>
                {
                    x.Span("Page ");
                    x.CurrentPageNumber();
                    x.Span(" of ");
                    x.TotalPages();
                });
            });
        });

        return document.GeneratePdf();
    }
}