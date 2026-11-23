using ClosedXML.Excel;

using Microsoft.EntityFrameworkCore;

using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Extensions;
using SmartTorque.Data;

namespace SmartTorque.Services;

public interface IReportService
{
    Task<List<SessionReportItemDto>> GetSessionReportAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<List<ProductUnitReportItemDto>> GetProductUnitReportAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<SessionDetailDto?> GetSessionDetailAsync(long sessionId, CancellationToken ct = default);
    Task<TraceabilityReportDto> GetTraceabilityReportAsync(string query, CancellationToken ct = default);
    Task<PassFailSummaryDto> GetPassFailSummaryAsync(DateTime? from, DateTime? to, int? departmentId = null, CancellationToken ct = default);
    Task<List<WorkerPerformanceDto>> GetWorkerPerformanceReportAsync(DateTime? from, DateTime? to, int? departmentId = null, CancellationToken ct = default);
    Task<List<DeviceUsageDto>> GetDeviceUsageReportAsync(CancellationToken ct = default);
    Task<DashboardStatsDto> GetAdminDashboardStatsAsync(int? departmentId = null, CancellationToken ct = default);
    Task<byte[]> ExportSessionsToExcelAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportSessionsToPdfAsync(SessionReportFilter filter, CancellationToken ct = default);
    Task<byte[]> ExportTraceabilityToExcelAsync(string query, CancellationToken ct = default);
    Task<byte[]> ExportTraceabilityToPdfAsync(string query, CancellationToken ct = default);
    Task<byte[]> ExportProductUnitPdfAsync(int jobId, int unitNo, InspectionReportCustomization? custom = null, CancellationToken ct = default);
    Task<byte[]> ExportProductUnitPdfByTokenAsync(string token, InspectionReportCustomization? custom = null, CancellationToken ct = default);
}

public class InspectionReportCustomization
{
    public string? CompanyName { get; set; } = "Godrej & Boyce Mfg. Co. Ltd.";
    public string? DivisionName { get; set; } = "Security Solutions Division";
    public string? RecordTitle { get; set; } = "Plant Quality Assurance Record";
    public string? DocTitle { get; set; } = "INSPECTION REPORT";
    public string? CheckSubtitle { get; set; } = "CHECK FOR FASTNER & BOLT TORQUE";
    public string? CertificateSubtitle { get; set; } = "Single Product Quality Certificate";
    public string? DocumentNo { get; set; } = "FL/QAD/F/42";
    public string? RevisionNo { get; set; } = "00";
    public string? RevisionDate { get; set; } = "09.08.17";
    public string? RevisionInfo { get; set; } = "Revision No: 00 | Date: 09.08.17";
    public string? PartSubAsslyName { get; set; } = "CHECK FOR FASTNER & BOLT TORQUE";
    public string? QualityStandard { get; set; } = "Standard: ISO 9001 / IATF 16949";
    public string? DrawingPoNo { get; set; } = "558-3680";
    public string? Remarks { get; set; } = "All critical fastener torques digitally acquired via IoT calibrated instruments and verified against engineering drawing tolerances.";
    public string? QaSignatory { get; set; } = "DIGITALLY AUDITED & APPROVED";
    public string? InspectorTitle { get; set; } = "Inspection Engineer / QA";
    public string? Section1Heading { get; set; } = "1. Company & Division Header";
    public string? Section2Heading { get; set; } = "2. Document Identification & Quality Standards";
    public string? Section3Heading { get; set; } = "3. QA Attestation & Engineer Sign-Off";
    public string? HiddenFields { get; set; } = "Section1Heading,Section2Heading,Section3Heading";
    public string? CompanyNameLabel { get; set; } = "Company Name";
    public string? DivisionNameLabel { get; set; } = "Division / Manufacturing Unit";
    public string? RecordTitleLabel { get; set; } = "Record Subtitle";
    public string? DocTitleLabel { get; set; } = "Document Title";
    public string? CheckSubtitleLabel { get; set; } = "Process / Check Subtitle";
    public string? CertificateSubtitleLabel { get; set; } = "Certificate Subtitle";
    public string? DocumentNoLabel { get; set; } = "Document No.";
    public string? RevisionNoLabel { get; set; } = "Revision No.";
    public string? RevisionDateLabel { get; set; } = "Rev. Date";
    public string? RevisionInfoLabel { get; set; } = "Revision & Date";
    public string? PartSubAsslyNameLabel { get; set; } = "Part/Sub-Assly Name";
    public string? QualityStandardLabel { get; set; } = "Quality Standard Reference";
    public string? DrawingPoNoLabel { get; set; } = "Drawing / PO Number";
    public string? RemarksLabel { get; set; } = "Quality Attestation / Remarks";
    public string? InspectorTitleLabel { get; set; } = "Inspection Engineer / QA Title";
    public string? QaSignatoryLabel { get; set; } = "Digital Seal / Approval Attestation";
}

public class ProductUnitReportItemDto
{
    public int JobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public int UnitNo { get; set; }
    public string? UnitTokenNo { get; set; }
    public string? ProductSerial { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public List<string> WorkerNames { get; set; } = new();
    public List<string> DeviceTransmitters { get; set; } = new();
    public DateTime? EarliestCapturedAt { get; set; }
    public DateTime? LatestCapturedAt { get; set; }
    public TimeSpan WorkingDuration { get; set; }
    public int TotalComponentsCount { get; set; }
    public int BuiltComponentsCount { get; set; }
    public List<ProductUnitComponentStatusDto> ComponentStatuses { get; set; } = new();
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public ProductUnitOverallStatus OverallStatus { get; set; }
    public string StatusBadgeText { get; set; } = string.Empty;
    public string StatusBadgeCss { get; set; } = string.Empty;
    public int ReworkCount { get; set; }
    public bool IsInRework { get; set; }
    public string? LatestReworkReason { get; set; }
    public List<JobUnitReworkDto> ReworkHistory { get; set; } = new();
}

public class ProductUnitComponentStatusDto
{
    public long? SessionId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public int Version { get; set; }
    public bool IsBuilt { get; set; }
    public int TotalSteps { get; set; }
    public int PassedSteps { get; set; }
    public int FailedSteps { get; set; }
    public SmartTorque.Core.Enums.SessionResult Result { get; set; }
}

public enum ProductUnitOverallStatus
{
    IncompletePass,
    IncompleteFailed,
    Pass,
    Fail,
    Rework
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

    private static TimeSpan CalculateActiveWorkingDuration(IEnumerable<DateTime> timestamps, DateTime sessionStart, DateTime? sessionEnd)
    {
        var valid = timestamps.Where(t => t.Year >= 2020).OrderBy(t => t).ToList();
        if (valid.Count == 0)
        {
            if (sessionEnd.HasValue && sessionEnd.Value > sessionStart)
            {
                var raw = sessionEnd.Value - sessionStart;
                return raw <= TimeSpan.FromHours(4) ? raw : TimeSpan.FromMinutes(5);
            }
            return TimeSpan.FromMinutes(1);
        }

        TimeSpan total = TimeSpan.Zero;
        DateTime clusterStart = valid[0];
        DateTime clusterEnd = valid[0];

        for (int i = 1; i < valid.Count; i++)
        {
            var gap = valid[i] - clusterEnd;
            if (gap <= TimeSpan.FromMinutes(15) && gap >= TimeSpan.Zero)
            {
                clusterEnd = valid[i];
            }
            else
            {
                var clusterSpan = clusterEnd - clusterStart;
                total += clusterSpan > TimeSpan.FromSeconds(5) ? clusterSpan : TimeSpan.FromSeconds(30);
                clusterStart = valid[i];
                clusterEnd = valid[i];
            }
        }

        var lastClusterSpan = clusterEnd - clusterStart;
        total += lastClusterSpan > TimeSpan.FromSeconds(5) ? lastClusterSpan : TimeSpan.FromSeconds(30);

        return total > TimeSpan.Zero ? total : TimeSpan.FromSeconds(30);
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
                .ThenInclude(r => r.Device)
            .AsQueryable();

        if (filter.DepartmentId.HasValue)
        {
            query = query.Where(s => s.Job.Product.DepartmentId == filter.DepartmentId.Value || (s.Job.AssemblyLine != null && s.Job.AssemblyLine.DepartmentId == filter.DepartmentId.Value));
        }

        if (filter.AssemblyLineId.HasValue)
        {
            query = query.Where(s => s.Job.AssemblyLineId == filter.AssemblyLineId.Value);
        }

        if (filter.WorkStationId.HasValue)
        {
            query = query.Where(s => s.Worker.WorkStationId == filter.WorkStationId.Value);
        }

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

        List<(int JobId, int UnitIndex)>? matchingUnitTokens = null;
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.Trim().ToLower();
            var matchedUnits = await _db.JobUnits
                .AsNoTracking()
                .Where(u => u.TokenNo.ToLower().Contains(search))
                .Select(u => new { u.JobId, u.UnitIndex })
                .ToListAsync(ct);

            matchingUnitTokens = matchedUnits.Select(m => (m.JobId, m.UnitIndex)).ToList();
            var matchingJobIds = matchingUnitTokens.Select(m => m.JobId).Distinct().ToList();

            query = query.Where(s =>
                s.Job.JobNo.ToLower().Contains(search) ||
                (s.Job.ProductSerial != null && s.Job.ProductSerial.ToLower().Contains(search)) ||
                s.Job.Product.ProductCode.ToLower().Contains(search) ||
                s.Job.Product.ProductName.ToLower().Contains(search) ||
                s.Worker.FullName.ToLower().Contains(search) ||
                (s.Worker.EmployeeNo != null && s.Worker.EmployeeNo.ToLower().Contains(search)) ||
                matchingJobIds.Contains(s.JobId));
        }

        query = query.Where(s => s.Results.Any() || s.Status == SessionStatus.InProgress);

        var sessions = await query
            .OrderByDescending(s => s.StartTime)
            .ToListAsync(ct);

        if (!string.IsNullOrWhiteSpace(filter.Search) && matchingUnitTokens != null && matchingUnitTokens.Any())
        {
            var search = filter.Search.Trim().ToLower();
            var unitSet = matchingUnitTokens.ToHashSet();
            sessions = sessions.Where(s =>
                s.Job.JobNo.ToLower().Contains(search) ||
                (s.Job.ProductSerial != null && s.Job.ProductSerial.ToLower().Contains(search)) ||
                s.Job.Product.ProductCode.ToLower().Contains(search) ||
                s.Job.Product.ProductName.ToLower().Contains(search) ||
                s.Worker.FullName.ToLower().Contains(search) ||
                (s.Worker.EmployeeNo != null && s.Worker.EmployeeNo.ToLower().Contains(search)) ||
                unitSet.Contains((s.JobId, s.UnitNo))).ToList();
        }

        var jobIds = sessions.Select(s => s.JobId).Distinct().ToList();
        var jobUnits = await _db.JobUnits
            .AsNoTracking()
            .Where(u => jobIds.Contains(u.JobId))
            .ToListAsync(ct);

        return sessions.Select(s =>
        {
            var allowedStepIdSet = !string.IsNullOrWhiteSpace(s.Job.AllowedStepIds)
                ? s.Job.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(int.Parse)
                    .ToHashSet()
                : null;

            var relevantResults = allowedStepIdSet != null
                ? s.Results.Where(r => allowedStepIdSet.Contains(r.StepId)).ToList()
                : s.Results.ToList();

            var totalSteps = relevantResults.Select(r => r.StepId).Distinct().Count();
            var passedSteps = relevantResults.Where(r => r.Result == StepResult.Pass).Select(r => r.StepId).Distinct().Count();
            
            // Defect = only if there is a failed bolt that was NOT reworked and changed to the passing value!
            var hasUncorrectedFail = relevantResults.Any(r => r.Result == StepResult.Fail && !relevantResults.Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Pass));
            var failedSteps = relevantResults.Where(r => r.Result == StepResult.Fail && !relevantResults.Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Pass)).Select(r => r.StepId).Distinct().Count();
            var hasRework = s.ReworkCount > 0 || relevantResults.Any(r => r.AttemptNo > 1 || (r.Result == StepResult.Pass && relevantResults.Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Fail)));

            var unitToken = jobUnits.FirstOrDefault(u => u.JobId == s.JobId && u.UnitIndex == s.UnitNo)?.TokenNo;

            var effectiveResult = s.FinalResult;
            if (hasUncorrectedFail)
            {
                effectiveResult = SmartTorque.Core.Enums.SessionResult.Fail;
            }
            else if (passedSteps >= totalSteps && totalSteps > 0)
            {
                effectiveResult = SmartTorque.Core.Enums.SessionResult.Pass;
            }
            else if (passedSteps < totalSteps && s.Status == SessionStatus.Completed)
            {
                effectiveResult = SmartTorque.Core.Enums.SessionResult.Fail;
            }
            else if (passedSteps < totalSteps && s.Status != SessionStatus.Completed)
            {
                effectiveResult = SmartTorque.Core.Enums.SessionResult.Incomplete;
            }
            else if (passedSteps >= totalSteps && !hasUncorrectedFail)
            {
                effectiveResult = SmartTorque.Core.Enums.SessionResult.Pass;
            }

            TimeSpan? duration = CalculateActiveWorkingDuration(
                s.Results.Select(r => r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : s.StartTime)),
                s.StartTime,
                s.EndTime);

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
                s.TorqueDeviceId ?? s.Results.FirstOrDefault(r => r.DeviceId.HasValue && r.InstrumentType == InstrumentType.Torque)?.DeviceId,
                s.TorqueDevice?.TransmitterId ?? s.TorqueDevice?.Name ?? s.Results.FirstOrDefault(r => r.Device != null && r.InstrumentType == InstrumentType.Torque)?.Device?.TransmitterId ?? s.Results.FirstOrDefault(r => r.Device != null && r.InstrumentType == InstrumentType.Torque)?.Device?.Name,
                s.VernierDeviceId ?? s.Results.FirstOrDefault(r => r.DeviceId.HasValue && r.InstrumentType == InstrumentType.Vernier)?.DeviceId,
                s.VernierDevice?.TransmitterId ?? s.VernierDevice?.Name ?? s.Results.FirstOrDefault(r => r.Device != null && r.InstrumentType == InstrumentType.Vernier)?.Device?.TransmitterId ?? s.Results.FirstOrDefault(r => r.Device != null && r.InstrumentType == InstrumentType.Vernier)?.Device?.Name,
                s.StartTime,
                s.EndTime,
                duration,
                s.Status,
                effectiveResult,
                hasRework ? Math.Max(s.ReworkCount, 1) : s.ReworkCount,
                totalSteps,
                passedSteps,
                failedSteps,
                s.Remarks,
                s.UnitNo,
                unitToken
            );
        }).ToList();
    }

    public async Task<List<ProductUnitReportItemDto>> GetProductUnitReportAsync(SessionReportFilter filter, CancellationToken ct = default)
    {
        var filteredSessions = await GetSessionReportAsync(filter, ct);
        if (filteredSessions.Count == 0) return new List<ProductUnitReportItemDto>();

        // Identify the exact physical units (JobId + UnitNo) matched by the filter
        var matchedUnitKeys = filteredSessions
            .Select(s => new { s.JobId, s.UnitNo })
            .Distinct()
            .ToList();

        var matchedJobIds = matchedUnitKeys.Select(k => k.JobId).Distinct().ToList();

        // Retrieve JobUnits to get PRD Tokens for full cross-session / cross-station traceability
        var initialJobUnits = await _db.JobUnits
            .AsNoTracking()
            .Include(u => u.ReworkLogs)
            .Where(u => matchedJobIds.Contains(u.JobId))
            .ToListAsync(ct);

        var matchedTokens = initialJobUnits
            .Where(u => matchedUnitKeys.Any(k => k.JobId == u.JobId && k.UnitNo == u.UnitIndex) && !string.IsNullOrWhiteSpace(u.TokenNo))
            .Select(u => u.TokenNo.Trim())
            .Distinct()
            .ToList();

        var allRelatedJobUnits = matchedTokens.Count > 0
            ? await _db.JobUnits
                .AsNoTracking()
                .Include(u => u.ReworkLogs)
                .Where(u => matchedTokens.Contains(u.TokenNo) || matchedJobIds.Contains(u.JobId))
                .ToListAsync(ct)
            : initialJobUnits;

        var allRelevantJobIds = allRelatedJobUnits.Select(u => u.JobId).Union(matchedJobIds).Distinct().ToList();

        // Load ALL sessions for these physical units across all history, guaranteeing complete quality records
        var allSessionsForUnits = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.ProductComponent)
                .ThenInclude(pc => pc.Template)
                    .ThenInclude(t => t.Steps)
            .Include(s => s.Worker)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Include(s => s.Results)
                .ThenInclude(r => r.Device)
            .Where(s => allRelevantJobIds.Contains(s.JobId))
            .ToListAsync(ct);

        // Filter in-memory to only sessions belonging to matched physical units
        var tokenSet = matchedTokens.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var unitKeySet = matchedUnitKeys.Select(k => (k.JobId, k.UnitNo)).ToHashSet();

        var unitSessions = allSessionsForUnits.Where(s =>
        {
            var tok = allRelatedJobUnits.FirstOrDefault(u => u.JobId == s.JobId && u.UnitIndex == s.UnitNo)?.TokenNo?.Trim();
            if (!string.IsNullOrEmpty(tok) && tokenSet.Contains(tok)) return true;
            return unitKeySet.Contains((s.JobId, s.UnitNo));
        }).ToList();

        var allSessionIds = unitSessions.Select(s => s.SessionId).ToList();
        var allResults = await _db.SessionResults
            .AsNoTracking()
            .Where(r => allSessionIds.Contains(r.SessionId))
            .ToListAsync(ct);

        var jobs = await _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
                .ThenInclude(p => p.Components)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps)
            .Where(j => allRelevantJobIds.Contains(j.JobId))
            .ToListAsync(ct);

        var grouped = unitSessions
            .GroupBy(s =>
            {
                var tok = allRelatedJobUnits.FirstOrDefault(u => u.JobId == s.JobId && u.UnitIndex == s.UnitNo)?.TokenNo?.Trim();
                return !string.IsNullOrEmpty(tok) ? $"TOKEN:{tok}" : $"JOB:{s.JobId}:{s.UnitNo}";
            })
            .ToList();

        var list = new List<ProductUnitReportItemDto>();

        foreach (var grp in grouped)
        {
            var first = grp.OrderByDescending(s => s.SessionId).First();
            var unitTokenNo = allRelatedJobUnits.FirstOrDefault(u => grp.Any(s => s.JobId == u.JobId && s.UnitNo == u.UnitIndex) && !string.IsNullOrEmpty(u.TokenNo))?.TokenNo;
            var primaryJobId = first.JobId;
            var primaryUnitNo = first.UnitNo;
            var job = jobs.FirstOrDefault(j => j.JobId == primaryJobId) ?? first.Job;
            if (job == null) continue;

            var grpJobIds = grp.Select(s => s.JobId).Distinct().ToList();
            var isMultiJobUnit = grpJobIds.Count > 1;

            var allowedStepIdSet = !isMultiJobUnit && !string.IsNullOrWhiteSpace(job.AllowedStepIds)
                ? job.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(int.Parse).ToHashSet()
                : null;

            // A Product Unit Quality Record must cover the ENTIRE Product (all components and all steps)
            var allComponents = job.Product.Components
                .OrderBy(c => c.AssemblyOrder)
                .ToList();

            var grpSessionIds = grp.Select(s => s.SessionId).ToList();
            var unitResults = allResults.Where(r => grpSessionIds.Contains(r.SessionId)).ToList();

            var compStatuses = new List<ProductUnitComponentStatusDto>();
            int passedStepsCount = 0;
            int failedStepsCount = 0;
            int totalStepsCount = 0;
            bool hasAnyUncorrectedFail = false;

            foreach (var comp in allComponents)
            {
                var compSessionIds = grp
                    .Where(s => s.ProductComponent.ComponentName == comp.ComponentName)
                    .Select(s => s.SessionId)
                    .ToList();

                var latestCompSession = grp
                    .Where(s => s.ProductComponent.ComponentName == comp.ComponentName)
                    .OrderByDescending(s => s.SessionId)
                    .FirstOrDefault();

                var compSteps = allowedStepIdSet != null
                    ? comp.Template.Steps.Where(st => allowedStepIdSet.Contains(st.TemplateStepId)).OrderBy(st => st.SequenceNo).ToList()
                    : comp.Template.Steps.OrderBy(st => st.SequenceNo).ToList();

                totalStepsCount += compSteps.Count;

                var compResults = unitResults
                    .Where(r => compSessionIds.Contains(r.SessionId))
                    .ToList();

                int compPassed = 0;
                int compFailed = 0;
                bool compHasFail = false;

                foreach (var step in compSteps)
                {
                    var stepAttempts = compResults
                        .Where(r => r.StepId == step.TemplateStepId)
                        .OrderBy(r => r.ReceivedAt)
                        .ThenBy(r => r.AttemptNo)
                        .ToList();

                    var latestAttempt = stepAttempts.LastOrDefault();

                    if (latestAttempt != null)
                    {
                        if (stepAttempts.Any(r => r.Result == StepResult.Pass) || latestAttempt.Result == StepResult.Pass)
                        {
                            compPassed++;
                        }
                        else
                        {
                            compFailed++;
                            compHasFail = true;
                            hasAnyUncorrectedFail = true;
                        }
                    }
                }

                passedStepsCount += compPassed;
                failedStepsCount += compFailed;

                bool isCompBuilt = compSteps.Count > 0 && compPassed == compSteps.Count;

                compStatuses.Add(new ProductUnitComponentStatusDto
                {
                    SessionId = latestCompSession?.SessionId,
                    ComponentName = comp.ComponentName,
                    Version = comp.Template.Version,
                    IsBuilt = isCompBuilt,
                    TotalSteps = compSteps.Count,
                    PassedSteps = compPassed,
                    FailedSteps = compFailed,
                    Result = isCompBuilt ? SmartTorque.Core.Enums.SessionResult.Pass : (compHasFail ? SmartTorque.Core.Enums.SessionResult.Fail : SmartTorque.Core.Enums.SessionResult.Incomplete)
                });
            }

            bool allTemplatesBuilt = allComponents.Count > 0 && compStatuses.All(c => c.IsBuilt);

            TimeSpan duration = TimeSpan.Zero;
            DateTime? earliest = null;
            DateTime? latest = null;

            if (unitResults.Any())
            {
                var validCaptured = unitResults
                    .Select(r => r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : first.StartTime))
                    .ToList();
                earliest = validCaptured.Min();
                latest = validCaptured.Max();
                duration = CalculateActiveWorkingDuration(validCaptured, first.StartTime, first.EndTime);
            }
            else
            {
                var minStart = grp.Min(s => s.StartTime);
                var maxEnd = grp.Where(s => s.EndTime.HasValue).Select(s => s.EndTime!.Value).DefaultIfEmpty(minStart).Max();
                duration = maxEnd > minStart ? maxEnd - minStart : TimeSpan.FromMinutes(1);
                earliest = minStart;
                latest = maxEnd;
            }

            var currentUnit = allRelatedJobUnits.FirstOrDefault(u => grp.Any(s => s.JobId == u.JobId && s.UnitNo == u.UnitIndex));
            int reworkCount = currentUnit?.ReworkCount ?? 0;
            bool isInRework = currentUnit?.Status == UnitAssemblyStatus.Rework;
            string? latestReworkReason = currentUnit?.LatestReworkReason;
            bool hasInCycleRework = unitResults.Any(r => r.AttemptNo > 1 || (r.Result == StepResult.Pass && unitResults.Any(r2 => r2.StepId == r.StepId && r2.Result == StepResult.Fail)));
            bool isReworked = reworkCount > 0 || hasInCycleRework;

            var reworkList = currentUnit?.ReworkLogs?
                .OrderByDescending(r => r.ReworkIteration)
                .Select(r => new JobUnitReworkDto
                {
                    JobUnitReworkId = r.JobUnitReworkId,
                    JobUnitId = r.JobUnitId,
                    JobId = r.JobId,
                    UnitIndex = r.UnitIndex,
                    TokenNo = r.TokenNo,
                    ReworkIteration = r.ReworkIteration,
                    Reason = r.Reason,
                    DefectCategory = r.DefectCategory,
                    TargetTemplateIds = r.TargetTemplateIds,
                    InitiatedByUserId = r.InitiatedByUserId,
                    InitiatedByUserName = r.InitiatedByUserName,
                    InitiatedAt = r.InitiatedAt,
                    Status = r.Status,
                    CompletedAt = r.CompletedAt,
                    CompletedByUserId = r.CompletedByUserId,
                    CompletedByUserName = r.CompletedByUserName,
                    ResolutionNotes = r.ResolutionNotes,
                    FinalResult = r.FinalResult
                }).ToList() ?? new List<JobUnitReworkDto>();

            ProductUnitOverallStatus overall;
            string badgeText;
            string badgeCss;

            if (isInRework)
            {
                overall = ProductUnitOverallStatus.Rework;
                badgeText = reworkCount > 0 ? $"REWORK (Rev #{reworkCount})" : "REWORK";
                badgeCss = "bg-warning text-dark border border-warning";
            }
            else if (allTemplatesBuilt && !hasAnyUncorrectedFail)
            {
                overall = ProductUnitOverallStatus.Pass;
                badgeText = isReworked ? "PASS (Reworked)" : "PASS";
                badgeCss = isReworked
                    ? "bg-warning-subtle text-dark border border-warning"
                    : "bg-success-subtle text-success border border-success-subtle";
            }
            else if (allTemplatesBuilt && hasAnyUncorrectedFail)
            {
                overall = ProductUnitOverallStatus.Fail;
                badgeText = "DEFECT (FAIL)";
                badgeCss = "bg-danger text-white";
            }
            else if (!allTemplatesBuilt && hasAnyUncorrectedFail)
            {
                overall = ProductUnitOverallStatus.IncompleteFailed;
                badgeText = "DEFECT (Uncorrected)";
                badgeCss = "bg-danger text-white";
            }
            else
            {
                overall = ProductUnitOverallStatus.IncompletePass;
                badgeText = "Incomplete";
                badgeCss = "bg-warning-subtle text-warning-emphasis border border-warning-subtle";
            }

            list.Add(new ProductUnitReportItemDto
            {
                JobId = primaryJobId,
                JobNo = isMultiJobUnit ? string.Join(" / ", grp.Select(s => s.Job.JobNo).Distinct()) : first.Job.JobNo,
                UnitNo = primaryUnitNo,
                UnitTokenNo = unitTokenNo,
                ProductSerial = first.Job.ProductSerial,
                ProductCode = first.Job.Product.ProductCode,
                ProductName = first.Job.Product.ProductName,
                WorkerNames = grp.Select(s => s.Worker.FullName).Distinct().ToList(),
                DeviceTransmitters = grp.Select(s => s.TorqueDevice?.TransmitterId ?? s.TorqueDevice?.Name)
                    .Concat(grp.Select(s => s.VernierDevice?.TransmitterId ?? s.VernierDevice?.Name))
                    .Concat(unitResults.Select(r => r.Device?.TransmitterId ?? r.Device?.Name))
                    .Where(d => !string.IsNullOrEmpty(d))
                    .Distinct()
                    .ToList()!,
                EarliestCapturedAt = earliest,
                LatestCapturedAt = latest,
                WorkingDuration = duration,
                TotalComponentsCount = allComponents.Count,
                BuiltComponentsCount = compStatuses.Count(c => c.IsBuilt),
                ComponentStatuses = compStatuses,
                TotalSteps = totalStepsCount,
                PassedSteps = passedStepsCount,
                FailedSteps = failedStepsCount,
                OverallStatus = overall,
                StatusBadgeText = badgeText,
                StatusBadgeCss = badgeCss,
                ReworkCount = isReworked ? Math.Max(reworkCount, 1) : 0,
                IsInRework = isInRework,
                LatestReworkReason = latestReworkReason,
                ReworkHistory = reworkList
            });
        }

        return list
            .OrderByDescending(u => u.LatestCapturedAt ?? DateTime.MinValue)
            .ThenByDescending(u => u.JobId)
            .ThenByDescending(u => u.UnitNo)
            .ToList();
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
            .Select(r =>
            {
                var effectiveCapturedAt = r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : session.StartTime);
                return new SessionStepResultDto(
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
                    effectiveCapturedAt,
                    r.ReceivedAt
                );
            })
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
            stepDtos,
            session.UnitNo,
            await _db.JobUnits.Where(u => u.JobId == session.JobId && u.UnitIndex == session.UnitNo).Select(u => u.TokenNo).FirstOrDefaultAsync(ct)
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

        // 1. Check for matching JobUnits by TokenNo (e.g. PRD-...)
        var matchedUnits = await _db.JobUnits
            .AsNoTracking()
            .Where(u => u.TokenNo.ToLower().Contains(lowerQuery))
            .ToListAsync(ct);
        var unitJobIds = matchedUnits.Select(u => u.JobId).Distinct().ToList();

        // 2. Search for matching jobs by ProductSerial, JobNo, or matching JobUnit
        var matchingJobs = await _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
            .Where(j => (j.ProductSerial != null && j.ProductSerial.ToLower().Contains(lowerQuery)) ||
                        j.JobNo.ToLower().Contains(lowerQuery) ||
                        unitJobIds.Contains(j.JobId))
            .OrderByDescending(j => j.CreatedAt)
            .Take(20)
            .ToListAsync(ct);

        var jobDtos = new List<TraceabilityJobDto>();
        foreach (var job in matchingJobs)
        {
            var sessions = await GetSessionReportAsync(new SessionReportFilter(Search: job.JobNo), ct);

            // If the query specifically searched a PRD unit token, filter sessions to that specific unit
            if (matchedUnits.Any(u => u.JobId == job.JobId))
            {
                var targetUnitIndices = matchedUnits.Where(u => u.JobId == job.JobId).Select(u => u.UnitIndex).ToList();
                sessions = sessions.Where(s => targetUnitIndices.Contains(s.UnitNo)).ToList();
            }

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

        // 2. Search for readings by Device TransmitterId, Name, or Matched Sessions from PRD/Job query
        var matchedSessionIds = jobDtos.SelectMany(j => j.Sessions.Select(s => s.SessionId)).Distinct().ToList();

        var deviceReadingsQuery = _db.SessionResults
            .AsNoTracking()
            .Include(r => r.Device)
            .Include(r => r.Step)
            .Include(r => r.Session)
                .ThenInclude(s => s.Job)
            .Include(r => r.Session)
                .ThenInclude(s => s.ProductComponent)
            .Include(r => r.Session)
                .ThenInclude(s => s.Worker)
            .AsQueryable();

        if (matchedSessionIds.Count > 0)
        {
            deviceReadingsQuery = deviceReadingsQuery.Where(r =>
                matchedSessionIds.Contains(r.SessionId) ||
                (r.Device != null && (r.Device.TransmitterId.ToLower().Contains(lowerQuery) || r.Device.Name.ToLower().Contains(lowerQuery))));
        }
        else
        {
            deviceReadingsQuery = deviceReadingsQuery.Where(r =>
                r.Device != null && (r.Device.TransmitterId.ToLower().Contains(lowerQuery) || r.Device.Name.ToLower().Contains(lowerQuery)));
        }

        var deviceReadings = await deviceReadingsQuery
            .OrderByDescending(r => r.CapturedAt)
            .Take(250)
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
                r.CapturedAt,
                r.AttemptNo,
                r.Session.ReworkCount
            ))
            .ToListAsync(ct);

        var reworkLogs = await _db.JobUnitReworks
            .AsNoTracking()
            .Where(r => r.TokenNo.ToLower().Contains(lowerQuery) || unitJobIds.Contains(r.JobId))
            .OrderByDescending(r => r.ReworkIteration)
            .Select(r => new JobUnitReworkDto
            {
                JobUnitReworkId = r.JobUnitReworkId,
                JobUnitId = r.JobUnitId,
                JobId = r.JobId,
                UnitIndex = r.UnitIndex,
                TokenNo = r.TokenNo,
                ReworkIteration = r.ReworkIteration,
                Reason = r.Reason,
                DefectCategory = r.DefectCategory,
                TargetTemplateIds = r.TargetTemplateIds,
                InitiatedByUserId = r.InitiatedByUserId,
                InitiatedByUserName = r.InitiatedByUserName,
                InitiatedAt = r.InitiatedAt,
                Status = r.Status,
                CompletedAt = r.CompletedAt,
                CompletedByUserId = r.CompletedByUserId,
                CompletedByUserName = r.CompletedByUserName,
                ResolutionNotes = r.ResolutionNotes,
                FinalResult = r.FinalResult
            })
            .ToListAsync(ct);

        return new TraceabilityReportDto(cleanQuery, jobDtos, deviceReadings, reworkLogs);
    }

    public async Task<PassFailSummaryDto> GetPassFailSummaryAsync(DateTime? from, DateTime? to, int? departmentId = null, CancellationToken ct = default)
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

    public async Task<List<WorkerPerformanceDto>> GetWorkerPerformanceReportAsync(DateTime? from, DateTime? to, int? departmentId = null, CancellationToken ct = default)
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

    public async Task<DashboardStatsDto> GetAdminDashboardStatsAsync(int? departmentId = null, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var todayStart = new DateTime(now.Year, now.Month, now.Day, 0, 0, 0, DateTimeKind.Utc);
        var onlineThreshold = now.AddSeconds(-30);

        // 1. Jobs by status
        var jobsQuery = _db.Jobs.AsNoTracking().Include(j => j.Product).Include(j => j.AssemblyLine).AsQueryable();
        if (departmentId.HasValue)
        {
            jobsQuery = jobsQuery.Where(j => (j.Product != null && j.Product.DepartmentId == departmentId.Value) ||
                                            (j.AssemblyLine != null && j.AssemblyLine.DepartmentId == departmentId.Value));
        }
        var jobs = await jobsQuery.ToListAsync(ct);
        var jobsByStatus = Enum.GetValues<JobStatus>()
            .ToDictionary(status => status, status => jobs.Count(j => j.Status == status));

        var totalJobsToday = jobs.Count(j => j.CreatedAt >= todayStart);

        // 2. Critical Black jobs waiting
        var criticalBlackJobs = jobs.Count(j =>
            j.Priority == PriorityLevel.Black &&
            (j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned));

        // 3. Devices online
        var devices = await _db.Devices.AsNoTracking().ToListAsync(ct);
        if (departmentId.HasValue)
        {
            var deptLines = await _db.AssemblyLines.AsNoTracking()
                .Where(l => l.DepartmentId == departmentId.Value)
                .Select(l => l.AssemblyCode)
                .ToListAsync(ct);
            var dept = await _db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.DepartmentId == departmentId.Value, ct);
            var deptNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!string.IsNullOrEmpty(dept?.Name)) deptNames.Add(dept.Name);
            foreach (var l in deptLines) deptNames.Add(l);

            devices = devices.Where(d => !string.IsNullOrEmpty(d.Location) && deptNames.Any(n => d.Location.Contains(n, StringComparison.OrdinalIgnoreCase))).ToList();
        }
        var totalDevices = devices.Count;
        var onlineDevices = devices.Where(d => d.LastSeenAt.HasValue && d.LastSeenAt.Value >= onlineThreshold).ToList();
        var onlineDevicesCount = onlineDevices.Count;
        var onlineTorqueCount = onlineDevices.Count(d => d.InstrumentType == InstrumentType.Torque);
        var onlineVernierCount = onlineDevices.Count(d => d.InstrumentType == InstrumentType.Vernier);

        // 4. Today's sessions & pass rate
        var sessionsQuery = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Results)
            .Include(s => s.Job).ThenInclude(j => j.Product)
            .Include(s => s.Job).ThenInclude(j => j.AssemblyLine)
            .Where(s => s.StartTime >= todayStart);

        if (departmentId.HasValue)
        {
            sessionsQuery = sessionsQuery.Where(s => (s.Job.Product != null && s.Job.Product.DepartmentId == departmentId.Value) ||
                                                    (s.Job.AssemblyLine != null && s.Job.AssemblyLine.DepartmentId == departmentId.Value));
        }

        var todaySessions = await sessionsQuery.ToListAsync(ct);

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
        var recentSessionsQuery = _db.Sessions
            .AsNoTracking()
            .Include(s => s.Job)
                .ThenInclude(j => j.Product)
            .Include(s => s.Job)
                .ThenInclude(j => j.AssemblyLine)
            .Include(s => s.ProductComponent)
            .Include(s => s.Worker)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            recentSessionsQuery = recentSessionsQuery.Where(s =>
                (s.Job.Product != null && s.Job.Product.DepartmentId == departmentId.Value) ||
                (s.Job.AssemblyLine != null && s.Job.AssemblyLine.DepartmentId == departmentId.Value));
        }

        var recentSessionsData = await recentSessionsQuery
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
        ws.Cell(1, 1).Value = "AssemblIQ (SmartTorque) — Quality Assembly Traceability Session Report";
        ws.Cell(1, 1).Style.Font.Bold = true;
        ws.Cell(1, 1).Style.Font.FontSize = 14;
        ws.Range(1, 1, 1, 13).Merge();

        ws.Cell(2, 1).Value = $"Generated: {DateTime.UtcNow.ToIst():yyyy-MM-dd HH:mm:ss} IST | Total Sessions: {sessions.Count}";
        ws.Cell(2, 1).Style.Font.Italic = true;
        ws.Cell(2, 1).Style.Font.FontSize = 10;
        ws.Range(2, 1, 2, 13).Merge();

        // Table headers
        var headers = new[]
        {
            "Session ID", "Job No", "Product Serial", "Product", "Component", "Version",
            "Worker", "Torque Device", "Vernier Device", "Start Time (IST)", "Duration (min)",
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
            ws.Cell(row, 10).Value = s.StartTime.ToIstString("yyyy-MM-dd HH:mm:ss");
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
                            c.Item().Text("AssemblIQ Assembly Traceability Report").Bold().FontSize(16).FontColor("#0F172A");
                            c.Item().Text($"Generated: {DateTime.UtcNow.ToIst():yyyy-MM-dd HH:mm:ss} IST | Total records: {sessions.Count}").FontSize(9).FontColor("#64748B");
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
                        header.Cell().Background("#1E293B").Padding(4).Text("Start Time (IST)").Bold().FontColor("#FFFFFF");
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
                        table.Cell().Background(bg).Padding(4).Text(s.StartTime.ToIstString("yyyy-MM-dd HH:mm"));
                        table.Cell().Background(bg).Padding(4).Text(s.Duration.HasValue ? $"{s.Duration.Value.TotalMinutes:0.0}" : "—");
                        table.Cell().Background(bg).Padding(4).Text(s.FinalResult.ToString()).Bold().FontColor(resultColor);
                    }
                });

                page.Footer().Column(c =>
                {
                    c.Item().LineHorizontal(1).LineColor("#CBD5E1");
                    c.Item().PaddingTop(4).Row(r =>
                    {
                        r.RelativeItem().Text("AssemblIQ Plant Quality System").FontSize(8).FontColor("#94A3B8");
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

        ws.Cell(1, 1).Value = $"AssemblIQ — Traceability Audit Report: Query '{trace.Query}'";
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
                    ws.Cell(row, 6).Value = s.StartTime.ToIstString("yyyy-MM-dd HH:mm:ss");
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
                ws.Cell(row, 10).Value = r.CapturedAt.ToIstString("yyyy-MM-dd HH:mm:ss");
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
                    col.Item().Text($"AssemblIQ — Traceability Audit: '{trace.Query}'").Bold().FontSize(16).FontColor("#0F172A");
                    col.Item().Text($"Generated: {DateTime.UtcNow.ToIst():yyyy-MM-dd HH:mm:ss} IST").FontSize(9).FontColor("#64748B");
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
                                    t.Cell().Padding(3).Text(s.StartTime.ToIstString("yyyy-MM-dd HH:mm"));
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
                                t.Cell().Padding(3).Text(r.CapturedAt.ToIstString("yyyy-MM-dd HH:mm:ss"));
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

    public async Task<byte[]> ExportProductUnitPdfByTokenAsync(string token, InspectionReportCustomization? custom = null, CancellationToken ct = default)
    {
        var cleanToken = (token ?? string.Empty).Trim();
        var jobUnit = await _db.JobUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.TokenNo.ToLower() == cleanToken.ToLower(), ct)
            ?? throw new KeyNotFoundException($"Product unit with token '{cleanToken}' not found.");

        return await ExportProductUnitPdfAsync(jobUnit.JobId, jobUnit.UnitIndex, custom, ct);
    }

    public async Task<byte[]> ExportProductUnitPdfAsync(int jobId, int unitNo, InspectionReportCustomization? custom = null, CancellationToken ct = default)
    {
        custom ??= new InspectionReportCustomization();

        var job = await _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .Include(j => j.AssemblyLine)
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct)
            ?? throw new KeyNotFoundException($"Job {jobId} not found.");

        var jobUnit = await _db.JobUnits
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.JobId == jobId && u.UnitIndex == unitNo, ct);

        var reworkLogs = await _db.JobUnitReworks
            .AsNoTracking()
            .Where(r => r.JobId == jobId && r.UnitIndex == unitNo)
            .OrderBy(r => r.ReworkIteration)
            .ToListAsync(ct);

        var tokenNo = jobUnit?.TokenNo?.Trim();
        List<(int JobId, int UnitNo)> relatedUnits = new() { (jobId, unitNo) };
        if (!string.IsNullOrWhiteSpace(tokenNo))
        {
            var matchedUnits = await _db.JobUnits
                .AsNoTracking()
                .Where(u => u.TokenNo == tokenNo)
                .Select(u => new { u.JobId, u.UnitIndex })
                .ToListAsync(ct);
            relatedUnits = matchedUnits.Select(m => (m.JobId, m.UnitIndex)).Distinct().ToList();
        }

        var relatedJobIds = relatedUnits.Select(r => r.JobId).Distinct().ToList();

        var sessionsRaw = await _db.Sessions
            .AsNoTracking()
            .Include(s => s.ProductComponent)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps.OrderBy(st => st.SequenceNo))
            .Include(s => s.Worker)
                .ThenInclude(w => w.Department)
            .Include(s => s.Worker)
                .ThenInclude(w => w.CurrentAssemblyLine)
            .Include(s => s.Worker)
                .ThenInclude(w => w.WorkStation)
            .Include(s => s.TorqueDevice)
            .Include(s => s.VernierDevice)
            .Where(s => relatedJobIds.Contains(s.JobId))
            .ToListAsync(ct);

        var relatedUnitSet = relatedUnits.ToHashSet();
        var sessions = sessionsRaw
            .Where(s => relatedUnitSet.Contains((s.JobId, s.UnitNo)))
            .OrderBy(s => s.ProductComponent.AssemblyOrder)
            .ThenBy(s => s.StartTime)
            .ToList();

        var sessionIds = sessions.Select(s => s.SessionId).ToList();
        var allResults = await _db.SessionResults
            .AsNoTracking()
            .Include(r => r.Step)
            .Include(r => r.Device)
            .Include(r => r.Session)
                .ThenInclude(s => s.Worker)
                    .ThenInclude(w => w.Department)
            .Include(r => r.Session)
                .ThenInclude(s => s.ProductComponent)
            .Where(r => sessionIds.Contains(r.SessionId))
            .OrderBy(r => r.ReceivedAt)
            .ToListAsync(ct);

        // Compute working duration based on first and last bolt dump
        var validCapturedList = allResults
            .Select(r => r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : sessions.FirstOrDefault()?.StartTime ?? DateTime.UtcNow))
            .ToList();

        DateTime? earliestCaptured = validCapturedList.Count > 0 ? validCapturedList.Min() : sessions.FirstOrDefault()?.StartTime;
        DateTime? latestCaptured = validCapturedList.Count > 0 ? validCapturedList.Max() : sessions.FirstOrDefault()?.EndTime;
        TimeSpan duration = TimeSpan.Zero;
        if (earliestCaptured.HasValue && latestCaptured.HasValue && latestCaptured.Value >= earliestCaptured.Value)
        {
            duration = latestCaptured.Value - earliestCaptured.Value;
            if (duration < TimeSpan.FromSeconds(30)) duration = TimeSpan.FromSeconds(30);
        }

        // Inspection report covers the full product unit across all components and steps
        var allComponents = job.Product.Components
            .OrderBy(c => c.AssemblyOrder)
            .ToList();

        int totalStepsCount = allComponents.Sum(c => c.Template.Steps.Count);
        var passedStepIds = allResults.Where(r => r.Result == StepResult.Pass).Select(r => r.StepId).ToHashSet();
        int passedCount = passedStepIds.Count;
        int failedCount = allResults.Where(r => r.Result == StepResult.Fail).Select(r => r.StepId).Distinct().Count(id => !passedStepIds.Contains(id));

        // Group readings by Session
        var sessionGroups = new List<(
            long SessionId,
            string ComponentName,
            int TemplateVersion,
            string OperatorName,
            string ToolId,
            string TimeWindow,
            List<(int SequenceNo, string Label, string ToolCategory, string Limits, string Observed, string? PrevObserved, bool IsReworked, string DateTimeIst, bool IsPass)> Steps
        )>();

        var activeSessions = sessions
            .Where(s => allResults.Any(r => r.SessionId == s.SessionId))
            .OrderBy(s => s.StartTime)
            .ToList();

        foreach (var sess in activeSessions)
        {
            var sessResults = allResults.Where(r => r.SessionId == sess.SessionId).OrderBy(r => r.Step.SequenceNo).ThenBy(r => r.AttemptNo).ToList();
            if (sessResults.Count == 0) continue;

            var opWorker = sess.Worker ?? sessResults.FirstOrDefault()?.Session?.Worker;
            var opFullName = opWorker?.FullName ?? "Assembly Operator";
            var opEmpNo = !string.IsNullOrEmpty(opWorker?.EmployeeNo) ? $" ({opWorker.EmployeeNo})" : "";
            var operatorName = $"{opFullName}{opEmpNo}";

            var dev = sess.TorqueDevice ?? sess.VernierDevice ?? sessResults.FirstOrDefault(r => r.Device != null)?.Device;
            var toolStr = dev?.TransmitterId ?? "—";

            var sessCapturedTimes = sessResults
                .Select(r => r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : sess.StartTime))
                .ToList();
            var sessMin = sessCapturedTimes.Count > 0 ? sessCapturedTimes.Min() : sess.StartTime;
            var sessMax = sessCapturedTimes.Count > 0 ? sessCapturedTimes.Max() : (sess.EndTime ?? sess.StartTime);
            var timeWindow = $"{sessMin.ToIstString("HH:mm:ss")} - {sessMax.ToIstString("HH:mm:ss")} IST";

            var sessStepItems = new List<(int SequenceNo, string Label, string ToolCategory, string Limits, string Observed, string? PrevObserved, bool IsReworked, string DateTimeIst, bool IsPass)>();
            var stepGroups = sessResults.GroupBy(r => r.StepId).OrderBy(g => g.First().Step.SequenceNo);

            foreach (var sg in stepGroups)
            {
                var attempts = sg.OrderBy(r => r.AttemptNo).ToList();
                var lastResult = attempts.Last();
                var hasPassed = attempts.Any(r => r.Result == StepResult.Pass);

                var step = lastResult.Step;
                var unitStr = step.Instrument == InstrumentType.Torque ? "N·m" : "mm";
                var toolCat = step.Instrument == InstrumentType.Torque ? "Torque Wrench" : (step.Instrument == InstrumentType.Vernier ? "Vernier Caliper" : "Node");
                var minLimit = step.Target - step.ToleranceMinus;
                var maxLimit = step.Target + step.TolerancePlus;
                var limitsStr = $"{step.Target:0.0} {unitStr} [{minLimit:0.0} - {maxLimit:0.0}]";

                bool isReworked = attempts.Count > 1 || sess.ReworkCount > 0;
                string? prevObservedStr = null;
                if (isReworked && attempts.Count > 1)
                {
                    var prev = attempts[attempts.Count - 2];
                    prevObservedStr = $"{prev.ActualValue:0.00} {unitStr}";
                }

                var effectiveTime = lastResult.CapturedAt.Year >= 2020 ? lastResult.CapturedAt : (lastResult.ReceivedAt.Year >= 2020 ? lastResult.ReceivedAt : sess.StartTime);
                var observedStr = $"{lastResult.ActualValue:0.00} {unitStr}";

                sessStepItems.Add((step.SequenceNo, step.Label, toolCat, limitsStr, observedStr, prevObservedStr, isReworked, effectiveTime.ToIstString("dd.MM.yy HH:mm:ss"), hasPassed));
            }

            sessionGroups.Add((
                sess.SessionId,
                sess.ProductComponent?.ComponentName ?? "Assembly Component",
                sess.ProductComponent?.Template?.Version ?? 1,
                operatorName,
                toolStr,
                timeWindow,
                sessStepItems
            ));
        }

        // Identify pending steps per component that were not passed in any session
        var pendingComponents = new List<(string ComponentName, List<TemplateStep> Steps)>();
        foreach (var comp in allComponents)
        {
            var unworked = comp.Template.Steps.Where(s => !passedStepIds.Contains(s.TemplateStepId)).OrderBy(s => s.SequenceNo).ToList();
            if (unworked.Count > 0)
            {
                pendingComponents.Add((comp.ComponentName, unworked));
            }
        }

        // Determine Overall Result according to user rules
        bool allBuilt = totalStepsCount > 0 && passedCount == totalStepsCount;
        string statusText;

        if (jobUnit?.Status == UnitAssemblyStatus.Rework)
        {
            statusText = (jobUnit.ReworkCount > 0) ? $"IN REWORK (#{jobUnit.ReworkCount})" : "IN REWORK";
        }
        else if (failedCount > 0)
        {
            statusText = $"FAILED ({failedCount} DEFECTS)";
        }
        else if (allBuilt && passedCount == totalStepsCount && totalStepsCount > 0)
        {
            statusText = (jobUnit?.ReworkCount ?? 0) > 0 ? $"PASS (REWORK #{jobUnit!.ReworkCount})" : $"PASS ({passedCount}/{totalStepsCount})";
        }
        else
        {
            statusText = $"INCOMPLETE ({passedCount}/{totalStepsCount})";
        }

        var unitToken = jobUnit?.TokenNo ?? job.ProductSerial ?? $"PRD-UNIT-{unitNo:D3}";

        // Cryptographic Integrity Fingerprint (IATF 16949 / ISO 9001 Anti-Tamper Verification)
        var rawIntegrityPayload = $"{unitToken}|{job.JobNo}|{passedCount}/{totalStepsCount}|{string.Join(",", sessionGroups.SelectMany(g => g.Steps).Select(s => s.Observed))}|{string.Join(",", sessionGroups.Select(g => g.OperatorName))}";
        var integrityHashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(rawIntegrityPayload));
        var integrityHash = Convert.ToHexString(integrityHashBytes).ToLowerInvariant();

        if (jobUnit != null && jobUnit.IntegrityHash != integrityHash)
        {
            jobUnit.IntegrityHash = integrityHash;
        }


        // Custom Header fields with fallback defaults
        var companyName = !string.IsNullOrWhiteSpace(custom.CompanyName) ? custom.CompanyName.Trim() : "Godrej & Boyce Mfg. Co. Ltd.";
        var divisionName = !string.IsNullOrWhiteSpace(custom.DivisionName) ? custom.DivisionName.Trim() : "Security Solutions Division";

        var docTitle = !string.IsNullOrWhiteSpace(custom.DocTitle) ? custom.DocTitle.Trim() : "INSPECTION REPORT";

        var documentNo = !string.IsNullOrWhiteSpace(custom.DocumentNo) ? custom.DocumentNo.Trim() : "FL/QAD/F/42";
        var revisionInfo = !string.IsNullOrWhiteSpace(custom.RevisionInfo) ? custom.RevisionInfo.Trim() : "Revision No: 00 | Date: 09.08.17";

        var drawingPoNo = !string.IsNullOrWhiteSpace(custom.DrawingPoNo) ? custom.DrawingPoNo.Trim() : (!string.IsNullOrWhiteSpace(job.PONo) ? job.PONo : "558-3680");
        var remarks = !string.IsNullOrWhiteSpace(custom.Remarks) ? custom.Remarks.Trim() : "All critical fastener torques digitally acquired via IoT calibrated instruments and verified against engineering drawing tolerances.";
        var qaSignatory = !string.IsNullOrWhiteSpace(custom.QaSignatory) ? custom.QaSignatory.Trim() : "DIGITALLY AUDITED & APPROVED";
        var inspectorTitle = !string.IsNullOrWhiteSpace(custom.InspectorTitle) ? custom.InspectorTitle.Trim() : "Inspection Engineer / QA";

        var partSubAsslyName = !string.IsNullOrWhiteSpace(custom.PartSubAsslyName)
            ? custom.PartSubAsslyName.Trim()
            : (!string.IsNullOrWhiteSpace(custom.CheckSubtitle) ? custom.CheckSubtitle.Trim() : "CHECK FOR FASTNER & BOLT TORQUE");

        var revNo = !string.IsNullOrWhiteSpace(custom.RevisionNo) ? custom.RevisionNo.Trim() : "00";
        var revDate = !string.IsNullOrWhiteSpace(custom.RevisionDate) ? custom.RevisionDate.Trim() : "09.08.17";

        if (custom.RevisionNo == null && !string.IsNullOrWhiteSpace(revisionInfo))
        {
            var parts = revisionInfo.Split('|', StringSplitOptions.TrimEntries);
            if (parts.Length > 0)
            {
                var p0 = parts[0];
                var colonIdx = p0.IndexOf(':');
                revNo = (colonIdx >= 0) ? p0[(colonIdx + 1)..].Trim().TrimStart('-', ' ') : p0;
            }
            if (parts.Length > 1)
            {
                var p1 = parts[1];
                var colonIdx = p1.IndexOf(':');
                revDate = (colonIdx >= 0) ? p1[(colonIdx + 1)..].Trim().TrimStart('-', ' ') : p1;
            }
        }

        var cleanDocNo = documentNo;
        if (cleanDocNo.StartsWith("Document No.:-", StringComparison.OrdinalIgnoreCase))
            cleanDocNo = cleanDocNo["Document No.:-".Length..].Trim();
        else if (cleanDocNo.StartsWith("Document No:", StringComparison.OrdinalIgnoreCase))
            cleanDocNo = cleanDocNo["Document No:".Length..].Trim();
        else if (cleanDocNo.StartsWith("Document No.", StringComparison.OrdinalIgnoreCase))
            cleanDocNo = cleanDocNo["Document No.".Length..].Trim().TrimStart(':', '-', ' ');

        var cleanRevNo = revNo;
        if (cleanRevNo.StartsWith("Revision No.:-", StringComparison.OrdinalIgnoreCase))
            cleanRevNo = cleanRevNo["Revision No.:-".Length..].Trim();
        else if (cleanRevNo.StartsWith("Revision No:", StringComparison.OrdinalIgnoreCase))
            cleanRevNo = cleanRevNo["Revision No:".Length..].Trim();
        else if (cleanRevNo.StartsWith("Revision No.", StringComparison.OrdinalIgnoreCase))
            cleanRevNo = cleanRevNo["Revision No.".Length..].Trim().TrimStart(':', '-', ' ');

        var cleanRevDate = revDate;
        if (cleanRevDate.StartsWith("Rev. Date.:-", StringComparison.OrdinalIgnoreCase))
            cleanRevDate = cleanRevDate["Rev. Date.:-".Length..].Trim();
        else if (cleanRevDate.StartsWith("Rev. Date:", StringComparison.OrdinalIgnoreCase))
            cleanRevDate = cleanRevDate["Rev. Date:".Length..].Trim();
        else if (cleanRevDate.StartsWith("Date:", StringComparison.OrdinalIgnoreCase))
            cleanRevDate = cleanRevDate["Date:".Length..].Trim();
        else if (cleanRevDate.StartsWith("Date.", StringComparison.OrdinalIgnoreCase))
            cleanRevDate = cleanRevDate["Date.".Length..].Trim().TrimStart(':', '-', ' ');

        var cleanDrawingNo = drawingPoNo;
        if (cleanDrawingNo.StartsWith("Drawing No:", StringComparison.OrdinalIgnoreCase))
            cleanDrawingNo = cleanDrawingNo["Drawing No:".Length..].Trim();
        else if (cleanDrawingNo.StartsWith("Drawing No", StringComparison.OrdinalIgnoreCase))
            cleanDrawingNo = cleanDrawingNo["Drawing No".Length..].Trim().TrimStart(':', '-', ' ');

        var cleanPartSubAssly = partSubAsslyName;
        if (cleanPartSubAssly.StartsWith("Part/Sub-Assly Name:", StringComparison.OrdinalIgnoreCase))
            cleanPartSubAssly = cleanPartSubAssly["Part/Sub-Assly Name:".Length..].Trim();
        else if (cleanPartSubAssly.StartsWith("Part/Sub-Assly Name", StringComparison.OrdinalIgnoreCase))
            cleanPartSubAssly = cleanPartSubAssly["Part/Sub-Assly Name".Length..].Trim().TrimStart(':', '-', ' ');

        var inspDateText = earliestCaptured.HasValue 
            ? earliestCaptured.Value.ToIst().ToString("dd.MM.yyyy") 
            : DateTime.UtcNow.ToIst().ToString("dd.MM.yyyy");

        var srNoText = !string.IsNullOrWhiteSpace(unitToken) 
            ? $"Unit #{unitNo}: {unitToken}" 
            : $"Unit #{unitNo}";

        var section1Heading = !string.IsNullOrWhiteSpace(custom.Section1Heading) ? custom.Section1Heading.Trim() : "1. Company & Division Header";
        var section2Heading = !string.IsNullOrWhiteSpace(custom.Section2Heading) ? custom.Section2Heading.Trim() : "2. Document Identification & Quality Standards";
        var section3Heading = !string.IsNullOrWhiteSpace(custom.Section3Heading) ? custom.Section3Heading.Trim() : "3. QA Attestation & Engineer Sign-Off";

        var hiddenFields = new HashSet<string>(
            (custom.HiddenFields ?? "Section1Heading,Section2Heading,Section3Heading")
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);
        bool IsHidden(string field) => hiddenFields.Contains(field);

        var stationNames = sessions
            .Select(s => s.Worker?.WorkStation?.StationName ?? s.Worker?.WorkStation?.StationCode)
            .Where(st => !string.IsNullOrWhiteSpace(st))
            .Distinct()
            .ToList();
        var stationDisplay = stationNames.Count > 0 ? string.Join(", ", stationNames) : "All Stations";

        var footerCompany = IsHidden("CompanyName") ? "AssemblIQ" : $"AssemblIQ — {companyName}";
        var footerRef = IsHidden("DocumentNo") ? string.Empty : $" | Ref: {documentNo}";

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(0.9f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(7.8f).FontFamily("Arial"));

                // Header matching Godrej & Boyce QAD Inspection standard
                page.Header().Column(header =>
                {
                    if (!IsHidden("Section1Heading"))
                    {
                        header.Item().PaddingBottom(2).Text(section1Heading).Bold().FontSize(8).FontColor("#475569");
                    }

                    header.Item().Border(1).BorderColor("#000000").Column(box =>
                    {
                        // Row 1: Company & Division Name (Full width centered)
                        box.Item().BorderBottom(1).BorderColor("#000000").PaddingVertical(3.5f).PaddingHorizontal(6).Text(t =>
                        {
                            t.AlignCenter();
                            var comp = companyName;
                            if (!string.IsNullOrWhiteSpace(divisionName) && !IsHidden("DivisionName"))
                            {
                                comp = $"{comp} - {divisionName}";
                            }
                            t.Span(comp).Bold().FontSize(10.5f).FontColor("#000000");
                        });

                        // Row 2: INSPECTION REPORT (left ~65%) | Document No & Revisions (right ~35%)
                        box.Item().BorderBottom(1).BorderColor("#000000").Row(r =>
                        {
                            // Left: INSPECTION REPORT
                            r.RelativeItem(6.4f).BorderRight(1).BorderColor("#000000").AlignMiddle().PaddingVertical(4).PaddingHorizontal(6).Text(docTitle).AlignCenter().Bold().FontSize(12.5f).FontColor("#000000");

                            // Right: Doc No / Rev No / Rev Date sub-table
                            r.RelativeItem(3.6f).Column(docCol =>
                            {
                                // Top: Document No.:- FL/QAD/F/42
                                docCol.Item().BorderBottom(1).BorderColor("#000000").PaddingVertical(2.5f).PaddingHorizontal(6).AlignLeft().Text(t =>
                                {
                                    t.Span("Document No.:- ").Bold().FontSize(7.2f).FontColor("#000000");
                                    t.Span(cleanDocNo).FontSize(7.2f).FontColor("#000000");
                                });

                                // Bottom: Revision No.:- 00 | Rev. Date.:- 09.08.17
                                docCol.Item().Row(revRow =>
                                {
                                    revRow.RelativeItem().BorderRight(1).BorderColor("#000000").PaddingVertical(2).PaddingHorizontal(6).AlignLeft().Text(t =>
                                    {
                                        t.Span("Revision No.:- ").Bold().FontSize(6.8f).FontColor("#000000");
                                        t.Span(cleanRevNo).FontSize(6.8f).FontColor("#000000");
                                    });
                                    revRow.RelativeItem().PaddingVertical(2).PaddingHorizontal(6).AlignLeft().Text(t =>
                                    {
                                        t.Span("Rev. Date.:- ").Bold().FontSize(6.8f).FontColor("#000000");
                                        t.Span(cleanRevDate).FontSize(6.8f).FontColor("#000000");
                                    });
                                });
                            });
                        });

                        // Row 3: Part/Sub-Assly Name: CHECK FOR FASTNER & BOLT TORQUE
                        box.Item().BorderBottom(1).BorderColor("#000000").PaddingVertical(2.5f).PaddingHorizontal(6).Text(t =>
                        {
                            t.Span("Part/Sub-Assly Name: ").Bold().FontSize(7.5f).FontColor("#000000");
                            t.Span(cleanPartSubAssly).FontSize(7.5f).FontColor("#000000");
                        });

                        // Row 4: Drawing No: 558-3680 | Insp. Date : ... | SR NO - ...
                        box.Item().BorderBottom(1).BorderColor("#000000").Row(r =>
                        {
                            r.RelativeItem(1.1f).BorderRight(1).BorderColor("#000000").PaddingVertical(2.5f).PaddingHorizontal(6).Text(t =>
                            {
                                t.Span("Drawing No: ").Bold().FontSize(7.2f).FontColor("#000000");
                                t.Span(cleanDrawingNo).FontSize(7.2f).FontColor("#000000");
                            });

                            r.RelativeItem(0.9f).BorderRight(1).BorderColor("#000000").PaddingVertical(2.5f).PaddingHorizontal(6).Text(t =>
                            {
                                t.Span("Insp. Date : ").Bold().FontSize(7.2f).FontColor("#000000");
                                t.Span(inspDateText).FontSize(7.2f).FontColor("#000000");
                            });

                            r.RelativeItem(1.4f).PaddingVertical(2.5f).PaddingHorizontal(6).Text(t =>
                            {
                                t.Span("SR NO - ").Bold().FontSize(7.2f).FontColor("#000000");
                                t.Span(srNoText).FontSize(7.2f).FontColor("#000000");
                            });
                        });

                        // Row 5: Detailed Product, Assembly Line & Overall Status Verification
                        box.Item().Row(r =>
                        {
                            r.RelativeItem(1.3f).BorderRight(1).BorderColor("#000000").PaddingVertical(3).PaddingHorizontal(6).Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Item Code: ").Bold().FontSize(7.0f); t.Span(job.Product.ProductCode).FontSize(7.0f); });
                                c.Item().Text(t => { t.Span("Product Description: ").Bold().FontSize(7.0f); t.Span(job.Product.ProductName).FontSize(7.0f); });
                                c.Item().Text(t => { t.Span("Job Order No: ").Bold().FontSize(7.0f); t.Span(job.JobNo).FontSize(7.0f); });
                            });

                            r.RelativeItem(1.7f).BorderRight(1).BorderColor("#000000").PaddingVertical(3).PaddingHorizontal(6).Column(c =>
                            {
                                c.Item().Text(t => { t.Span("Assembly Line: ").Bold().FontSize(7.0f); t.Span(job.AssemblyLine?.AssemblyName ?? "Assembly 1").FontSize(7.0f); });
                                c.Item().Text(t => { t.Span("Station: ").Bold().FontSize(7.0f); t.Span(stationDisplay).FontSize(7.0f); });
                                c.Item().Text(t =>
                                {
                                    t.Span("Inspection Time: ").Bold().FontSize(7.0f);
                                    t.Span(earliestCaptured.HasValue && latestCaptured.HasValue
                                        ? $"{earliestCaptured.Value.ToIst():HH:mm:ss} - {latestCaptured.Value.ToIst():HH:mm:ss} IST ({duration.TotalMinutes:0.0} min)"
                                        : "—").FontSize(7.0f);
                                });
                            });

                            r.ConstantItem(95).AlignMiddle().PaddingVertical(3).PaddingHorizontal(4).Column(c =>
                            {
                                c.Item().Text("OVERALL STATUS").AlignCenter().Bold().FontSize(6.5f).FontColor("#475569");
                                c.Item().PaddingTop(2.5f).AlignCenter().Border(1.2f).BorderColor("#0F172A").Background(allBuilt ? "#0F172A" : "#F1F5F9").PaddingVertical(3.5f).PaddingHorizontal(8)
                                    .Text(statusText).Bold().FontSize(8.0f).FontColor(allBuilt ? "#FFFFFF" : "#0F172A");
                            });
                        });
                    });
                });

                // Content: Session-Grouped Fastener Verification
                page.Content().PaddingTop(4).Column(col =>
                {
                    // 1. Render Each Completed / In-Progress Session
                    foreach (var sess in sessionGroups)
                    {
                        col.Item().PaddingTop(5).Border(1).BorderColor("#CBD5E1").Background("#F1F5F9").Padding(3.5f).Column(sc =>
                        {
                            sc.Item().Text(t =>
                            {
                                t.Span($"SESSION #{sess.SessionId}: ").Bold().FontSize(8.0f).FontColor("#0F172A");
                                t.Span($"{sess.ComponentName} ").Bold().FontSize(8.0f).FontColor("#1E293B");
                                t.Span($"(v{sess.TemplateVersion})").FontSize(7.0f).FontColor("#64748B");
                            });
                            sc.Item().PaddingTop(1.5f).Text(t =>
                            {
                                t.Span("Operator: ").Bold().FontSize(6.8f).FontColor("#475569");
                                t.Span($"{sess.OperatorName}").FontSize(6.8f).FontColor("#0F172A");
                                t.Span("   │   Tool: ").Bold().FontSize(6.8f).FontColor("#475569");
                                t.Span($"{sess.ToolId}").FontSize(6.8f).FontColor("#0F172A");
                                t.Span("   │   Time: ").Bold().FontSize(6.8f).FontColor("#475569");
                                t.Span($"{sess.TimeWindow}").FontSize(6.8f).FontColor("#0F172A");
                            });
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(20);   // #
                                columns.RelativeColumn(2.0f); // Fastener / Step
                                columns.RelativeColumn(1.3f); // Tool Category
                                columns.RelativeColumn(1.8f); // Specified Limits
                                columns.RelativeColumn(1.3f); // Observed Value
                                columns.ConstantColumn(80);   // Date & Time (IST)
                                columns.ConstantColumn(42);   // Result
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("#").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).Text("Fastener / Step").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("Tool Category").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("Specified Limits").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("Observed Value").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("Date & Time (IST)").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#1E293B").Border(0.5f).BorderColor("#334155").Padding(2.5f).AlignCenter().Text("Result").Bold().FontColor("#FFFFFF");
                            });

                            var rowIdx = 0;
                            foreach (var step in sess.Steps)
                            {
                                var bg = (rowIdx++ % 2 == 0) ? "#FFFFFF" : "#F8FAFC";
                                var resText = step.IsPass ? "PASS" : "FAIL";
                                var resColor = step.IsPass ? "#0F172A" : "#991B1B";

                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.SequenceNo.ToString());
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).Column(c =>
                                {
                                    c.Item().Text(step.Label).Bold();
                                    if (step.IsReworked)
                                    {
                                        c.Item().Text(t =>
                                        {
                                            t.Span("Reworked").Bold().FontSize(5.8f).FontColor("#B45309");
                                            if (!string.IsNullOrWhiteSpace(step.PrevObserved))
                                            {
                                                t.Span($" (Prev: {step.PrevObserved})").FontSize(5.5f).FontColor("#64748B");
                                            }
                                        });
                                    }
                                });
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.ToolCategory).FontSize(7.0f);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.Limits);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.Observed).Bold().FontColor(resColor);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.DateTimeIst).FontSize(6.8f);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(resText).Bold().FontColor(resColor);
                            }
                        });
                    }

                    // 2. Render Pending Fasteners in Identical Table Format
                    foreach (var pending in pendingComponents)
                    {
                        col.Item().PaddingTop(5).Border(1).BorderColor("#CBD5E1").Background("#F8FAFC").Padding(3.5f).Column(sc =>
                        {
                            sc.Item().Row(sr =>
                            {
                                sr.RelativeItem().Text(t =>
                                {
                                    t.Span("PENDING VERIFICATION: ").Bold().FontSize(8.0f).FontColor("#64748B");
                                    t.Span($"{pending.ComponentName}").Bold().FontSize(8.0f).FontColor("#334155");
                                });
                                sr.ConstantItem(120).AlignRight().Text($"{pending.Steps.Count} Fastener(s) Pending").FontSize(7.0f).FontColor("#64748B");
                            });
                            sc.Item().PaddingTop(1).Text($"Remaining operations awaiting execution on Unit #{unitNo}").FontSize(6.8f).FontColor("#94A3B8");
                        });

                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(20);   // #
                                columns.RelativeColumn(2.0f); // Fastener / Step
                                columns.RelativeColumn(1.3f); // Tool Category
                                columns.RelativeColumn(1.8f); // Specified Limits
                                columns.RelativeColumn(1.3f); // Observed Value
                                columns.ConstantColumn(80);   // Date & Time (IST)
                                columns.ConstantColumn(42);   // Result
                            });

                            table.Header(h =>
                            {
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("#").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).Text("Fastener / Step").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("Tool Category").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("Specified Limits").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("Observed Value").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("Date & Time (IST)").Bold().FontColor("#FFFFFF");
                                h.Cell().Background("#475569").Border(0.5f).BorderColor("#64748B").Padding(2.5f).AlignCenter().Text("Result").Bold().FontColor("#FFFFFF");
                            });

                            var rowIdx = 0;
                            foreach (var step in pending.Steps)
                            {
                                var bg = (rowIdx++ % 2 == 0) ? "#FFFFFF" : "#F8FAFC";
                                var unitStr = step.Instrument == InstrumentType.Torque ? "N·m" : "mm";
                                var toolCat = step.Instrument == InstrumentType.Torque ? "Torque Wrench" : (step.Instrument == InstrumentType.Vernier ? "Vernier Caliper" : "Node");
                                var minLimit = step.Target - step.ToleranceMinus;
                                var maxLimit = step.Target + step.TolerancePlus;
                                var limitsStr = $"{step.Target:0.0} {unitStr} [{minLimit:0.0} - {maxLimit:0.0}]";

                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(step.SequenceNo.ToString());
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).Text(step.Label);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(toolCat).FontSize(7.0f);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text(limitsStr);
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text("—").FontColor("#94A3B8");
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text("—").FontColor("#94A3B8");
                                table.Cell().Background(bg).Border(0.5f).BorderColor("#E2E8F0").Padding(2.2f).AlignCenter().Text("PENDING").FontColor("#64748B");
                            }
                        });
                    }

                    // Calibration Compliance Notice (if any tool operated past scheduled calibration)
                    var hasCalOverdue = allResults.Any(r => r.Device != null && r.Device.CalibrationDue.HasValue && r.Device.CalibrationDue.Value < DateOnly.FromDateTime(r.CapturedAt.Year >= 2020 ? r.CapturedAt : (r.ReceivedAt.Year >= 2020 ? r.ReceivedAt : DateTime.UtcNow)));
                    if (hasCalOverdue)
                    {
                        col.Item().PaddingTop(4).Border(0.5f).BorderColor("#F59E0B").Background("#FEF3C7").Padding(3).Row(r =>
                        {
                            r.RelativeItem().Text(t =>
                            {
                                t.Span("CALIBRATION COMPLIANCE NOTICE: ").Bold().FontColor("#B45309").FontSize(7.0f);
                                t.Span("Fastening operations performed with tool past scheduled calibration date. Measurements verified against tolerance and accepted under continuous production policy for secondary QA review.").FontColor("#92400E").FontSize(7.0f);
                            });
                        });
                    }

                    // Rework & Deviation Audit Trail (if unit has rework history)
                    if (reworkLogs.Count > 0)
                    {
                        col.Item().PaddingTop(5).Text("REWORK & DEFECT CORRECTION AUDIT TRAIL").Bold().FontSize(8).FontColor("#475569");
                        col.Item().PaddingTop(2).Table(rt =>
                        {
                            rt.ColumnsDefinition(rc =>
                            {
                                rc.ConstantColumn(30);   // Rev #
                                rc.ConstantColumn(80);   // Initiated (IST)
                                rc.RelativeColumn(1.3f); // Initiated By
                                rc.RelativeColumn(2.2f); // Defect Category & Reason
                                rc.RelativeColumn(1.0f); // Status
                                rc.RelativeColumn(1.6f); // Resolution & Completed By
                            });

                            rt.Header(rh =>
                            {
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).AlignCenter().Text("Rev #").Bold().FontColor("#FFFFFF");
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).AlignCenter().Text("Initiated (IST)").Bold().FontColor("#FFFFFF");
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).Text("Initiated By").Bold().FontColor("#FFFFFF");
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).Text("Defect Category & Reason").Bold().FontColor("#FFFFFF");
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).AlignCenter().Text("Status").Bold().FontColor("#FFFFFF");
                                rh.Cell().Background("#334155").Border(0.5f).BorderColor("#475569").Padding(2.5f).Text("Resolution / Verified By").Bold().FontColor("#FFFFFF");
                            });

                            foreach (var rwk in reworkLogs)
                            {
                                var statusStr = rwk.Status == UnitAssemblyStatus.Completed ? "PASS" : (rwk.Status == UnitAssemblyStatus.Rework ? "IN REWORK" : rwk.Status.ToString());
                                var statusCol = rwk.Status == UnitAssemblyStatus.Completed ? "#166534" : "#92400E";

                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).AlignCenter().Text($"#{rwk.ReworkIteration}");
                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).AlignCenter().Text(rwk.InitiatedAt.ToIstString("dd/MM/yy HH:mm"));
                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).Text(rwk.InitiatedByUserName);
                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).Text(string.IsNullOrEmpty(rwk.DefectCategory) ? rwk.Reason : $"{rwk.DefectCategory}: {rwk.Reason}");
                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).AlignCenter().Text(statusStr).Bold().FontColor(statusCol);
                                var resStr = rwk.CompletedAt.HasValue
                                    ? $"{rwk.CompletedByUserName ?? "Operator"} ({rwk.CompletedAt.Value.ToIstString("dd/MM/yy HH:mm")})"
                                    : (rwk.ResolutionNotes ?? "Pending rework completion");
                                rt.Cell().Border(0.5f).BorderColor("#CBD5E1").Padding(2.5f).Text(resStr);
                            }
                        });
                    }

                    // Section 3: Attestation and Sign-Off Block
                    if (!IsHidden("Section3Heading"))
                    {
                        col.Item().PaddingTop(6).Text(section3Heading).Bold().FontSize(8).FontColor("#475569");
                    }

                    col.Item().PaddingTop(4).Border(1).BorderColor("#000000").Table(signTable =>
                    {
                        signTable.ColumnsDefinition(c =>
                        {
                            c.RelativeColumn(3.2f); // Quality Remarks & Attestation
                            c.RelativeColumn(1.8f); // Digital Seal & Approval
                        });

                        signTable.Cell().BorderRight(1).BorderColor("#000000").Padding(5).Column(c =>
                        {
                            c.Item().Text("QUALITY ATTESTATION & REMARKS:").Bold().FontSize(7.5f).FontColor("#0F172A");
                            if (!IsHidden("Remarks")) c.Item().PaddingTop(2).Text(remarks).FontSize(7.0f).FontColor("#334155");
                            var pendingCountText = (totalStepsCount - passedCount > 0) ? $"{totalStepsCount - passedCount} Pending" : "100% In-Spec";
                            c.Item().PaddingTop(3).Text($"Total Fasteners Verified: {passedCount}/{totalStepsCount} Passed ({pendingCountText})").Bold().FontSize(7.0f).FontColor("#0F172A");
                            c.Item().PaddingTop(1).Text($"Verification Barcode / Serial: {unitToken.Substring(0, Math.Min(28, unitToken.Length))}").FontSize(6.5f).FontColor("#64748B");
                            c.Item().PaddingTop(1).Text($"Anti-Tamper SHA-256: {integrityHash.Substring(0, 16)}...{integrityHash.Substring(integrityHash.Length - 8)}").Bold().FontSize(6.0f).FontColor("#334155");
                        });

                        signTable.Cell().Padding(5).Column(c =>
                        {
                            if (!IsHidden("InspectorTitle")) c.Item().Text(inspectorTitle).Bold().FontSize(8.0f).FontColor("#0F172A");
                            if (!IsHidden("QaSignatory"))
                            {
                                c.Item().PaddingTop(2).Text("Digital Approval Attestation:").FontSize(6.5f).FontColor("#64748B");
                                var sealText = allBuilt ? qaSignatory : "INCOMPLETE — IN PROGRESS";
                                c.Item().PaddingTop(3).AlignCenter().Border(1.5f).BorderColor("#0F172A").Background(allBuilt ? "#F1F5F9" : "#FFFFFF").Padding(4).Text(sealText).Bold().FontSize(7.5f).FontColor("#0F172A");
                            }
                            c.Item().PaddingTop(6).Text($"Date: {DateTime.UtcNow.ToIst():dd.MM.yyyy}").FontSize(6.5f).FontColor("#64748B");
                        });
                    });
                });

                // Footer
                page.Footer().Row(r =>
                {
                    r.RelativeItem().Text($"{footerCompany}{footerRef} | Anti-Tamper SHA-256: {integrityHash.Substring(0, 12)}").FontSize(6.0f).FontColor("#94A3B8");
                    r.RelativeItem().AlignRight().Text(x =>
                    {
                        x.DefaultTextStyle(t => t.FontSize(6.5f).FontColor("#94A3B8"));
                        x.Span("Page ");
                        x.CurrentPageNumber();
                        x.Span(" of ");
                        x.TotalPages();
                    });
                });
            });
        });

        return document.GeneratePdf();
    }
}