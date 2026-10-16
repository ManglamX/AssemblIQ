using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>Outcome of confirming a BM preview (Architecture §7.3 step 5, BM-6).</summary>
public record ConfirmImportResult(
    int UploadId,
    int JobsCreated,
    IReadOnlyList<string> JobNumbers,
    int BlackJobs,
    bool NotificationRaised,
    int JobsRefreshed = 0,
    IReadOnlyList<string>? RefreshedJobNumbers = null,
    int AbsentJobsCount = 0)
{
    public IReadOnlyList<string> RefreshedJobNumbers { get; init; } = RefreshedJobNumbers ?? [];
}

/// <summary>Preview comparison of how a BM upload matches existing open jobs (BM-6).</summary>
public sealed record BmRefreshPreviewResult(
    int UploadId,
    IReadOnlyDictionary<int, BmRowJobMatch> MatchedRows,
    IReadOnlyList<Job> AbsentOpenJobs);

/// <summary>Details of an existing open job matched to an incoming BM row.</summary>
public sealed record BmRowJobMatch(
    int JobId,
    string JobNo,
    PriorityLevel ExistingPriority,
    PriorityLevel NewPriority,
    int ExistingQuantity,
    int NewQuantity,
    JobStatus CurrentStatus,
    string? AssignedWorkerName)
{
    public bool HasPriorityChanged => ExistingPriority != NewPriority;
    public bool HasQuantityChanged => ExistingQuantity != NewQuantity;
}

/// <summary>Filters for one workload-board query (PRD BM-9, Design 7.7).</summary>
public sealed record WorkloadFilter
{
    /// <summary>Tick priorities; empty = every priority.</summary>
    public IReadOnlyList<PriorityLevel> Priorities { get; init; } = [];

    /// <summary>Inclusive lower DueDate bound; null = no lower bound.</summary>
    public DateOnly? DueFrom { get; init; }

    /// <summary>Inclusive upper DueDate bound; null = no upper bound.</summary>
    public DateOnly? DueTo { get; init; }

    /// <summary>Exact job status; null = every status.</summary>
    public JobStatus? Status { get; init; }

    /// <summary>Only jobs assigned to this worker; null = any worker.</summary>
    public string? AssignedWorkerId { get; init; }

    /// <summary><c>true</c> = only jobs with no worker ("Open" in the filter bar).</summary>
    public bool UnassignedOnly { get; init; }
}

/// <summary>One page of the workload board, already ordered (PRD BM-9).</summary>
public sealed record WorkloadResult(IReadOnlyList<Job> Jobs, int Total);

/// <summary>Summary of manufacturing jobs aggregated by Item Code across all BMs.</summary>
public sealed record ItemWorkloadSummary
{
    public string ItemCode { get; init; } = string.Empty;
    public int? ProductId { get; init; }
    public string? ProductName { get; init; }
    public string? ProductDescription { get; init; }
    public bool ProductConfigured => ProductId.HasValue;
    public int ComponentCount { get; init; }
    public int TotalSteps { get; init; }
    public int TotalQuantityRequired { get; init; }
    public int TotalQuantityCompleted { get; init; }
    public int TotalQuantityRemaining => Math.Max(0, TotalQuantityRequired - TotalQuantityCompleted);
    public int TotalJobs { get; init; }
    public int TotalOrders => OrderBifurcations.Count;
    public PriorityLevel HighestPriority { get; init; } = PriorityLevel.White;
    public DateOnly? EarliestDueDate { get; init; }
    public IReadOnlyList<ItemJobSummary> Jobs { get; init; } = [];
    public IReadOnlyList<ItemOrderBifurcation> OrderBifurcations { get; init; } = [];
}

/// <summary>Order-level breakdown of jobs for an Item Code grouped by Sales Order and PO No.</summary>
public sealed record ItemOrderBifurcation
{
    public string? SalesOrder { get; init; }
    public string? PONo { get; init; }
    public string DisplaySalesOrder => string.IsNullOrWhiteSpace(SalesOrder) ? "Unspecified Sales Order" : SalesOrder;
    public string DisplayPONo => string.IsNullOrWhiteSpace(PONo) ? "—" : PONo;
    public int QuantityRequired { get; init; }
    public int QuantityCompleted { get; init; }
    public int QuantityRemaining => Math.Max(0, QuantityRequired - QuantityCompleted);
    public double CompletionPercentage => QuantityRequired > 0 ? Math.Round((double)QuantityCompleted / QuantityRequired * 100, 1) : 0;
    public PriorityLevel HighestPriority { get; init; } = PriorityLevel.White;
    public DateOnly? EarliestDueDate { get; init; }
    public int JobCount => Jobs.Count;
    public IReadOnlyList<ItemJobSummary> Jobs { get; init; } = [];
}

/// <summary>One job detail row under an Item Code summary.</summary>
public sealed record ItemJobSummary
{
    public int JobId { get; init; }
    public string JobNo { get; init; } = string.Empty;
    public string? SalesOrder { get; init; }
    public string? PONo { get; init; }
    public int Quantity { get; init; }
    public int QuantityCompleted { get; init; }
    public PriorityLevel Priority { get; init; }
    public DateOnly? DueDate { get; init; }
    public JobStatus Status { get; init; }
    public string? AssignedWorkerName { get; init; }
    public string? BmFileName { get; init; }
    public int? BmUploadId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Turns a reviewed BM preview into Jobs (PRD BM-5, BM-10; Architecture §7.3 step 5).
/// Everything the import touches — the jobs, the rows it consumed, the upload's new
/// status, the audit entry and the Black-job notice — is written in one
/// <c>SaveChanges</c>, so a confirm either happens completely or not at all.
/// </summary>
public interface IJobImportService
{
    /// <summary>
    /// Creates one Job per importable row (Ok or Warning) and marks the upload Confirmed.
    /// Throws <see cref="InvalidOperationException"/> when the upload is not awaiting a
    /// confirm, when nothing is importable, or when a row's product has become inactive
    /// since the preview — nothing is written in any of those cases.
    /// </summary>
    Task<ConfirmImportResult> ConfirmAsync(
        int uploadId,
        string confirmedBy,
        CancellationToken ct = default);

    /// <summary>Job numbers created from an upload, in the order they were allocated.</summary>
    Task<IReadOnlyList<string>> GetJobNumbersAsync(int uploadId, CancellationToken ct = default);

    /// <summary>
    /// One filtered page of the workload board, ordered by
    /// <see cref="IPriorityService.OrderForWorkboard"/> (PRD BM-9; Architecture §7.2 -
    /// the ordering rule is implemented once and reused here).
    /// </summary>
    Task<WorkloadResult> GetWorkloadAsync(
        WorkloadFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default);

    /// <summary>
    /// Aggregates manufacturing jobs grouped by Product Item Code across all BMs with total quantities,
    /// progress, and individual jobs breakdown.
    /// </summary>
    Task<IReadOnlyList<ItemWorkloadSummary>> GetWorkloadByItemAsync(
        string? search = null,
        CancellationToken ct = default);

    /// <summary>
    /// Assigns a job to a specific worker or unassigns it to Open. Optionally overrides priority.
    /// </summary>
    Task<Job> AssignJobAsync(
        int jobId,
        string? workerId,
        PriorityLevel? overridePriority,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>
    /// Automatically distributes open/unassigned jobs across active workers with workload balancing.
    /// </summary>
    Task<int> AutoAssignJobsAsync(
        string changedBy,
        CancellationToken ct = default);

    /// <summary>Places a job on hold.</summary>
    Task<Job> HoldJobAsync(
        int jobId,
        string? reason,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>Resumes a job from hold.</summary>
    Task<Job> ResumeJobAsync(
        int jobId,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>Cancels a job.</summary>
    Task<Job> CancelJobAsync(
        int jobId,
        string? reason,
        string changedBy,
        CancellationToken ct = default);

    /// <summary>Loads a job by Id with full Product, Components, Templates, Steps, Worker, and Source BM information.</summary>
    Task<Job?> GetJobByIdAsync(int jobId, CancellationToken ct = default);

    /// <summary>Loads a job by JobNo with full details.</summary>
    Task<Job?> GetJobByNoAsync(string jobNo, CancellationToken ct = default);

    /// <summary>Returns the audit logs associated with a specific Job.</summary>
    Task<IReadOnlyList<AuditLog>> GetJobAuditLogsAsync(int jobId, CancellationToken ct = default);

    /// <summary>Returns open jobs (Pending, Assigned, OnHold) whose (ProductCode, DueDate) are absent from the specified upload.</summary>
    Task<IReadOnlyList<Job>> GetAbsentJobsAsync(int uploadId, CancellationToken ct = default);

    /// <summary>Computes preview match information for an upload: which rows match open jobs to refresh, and which open jobs are absent.</summary>
    Task<BmRefreshPreviewResult> GetRefreshPreviewAsync(int uploadId, CancellationToken ct = default);
}

public class JobImportService : IJobImportService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IPriorityService _priorities;

    public JobImportService(AppDbContext db, INotificationService notifications, IPriorityService priorities)
    {
        _db = db;
        _notifications = notifications;
        _priorities = priorities;
    }

    public async Task<ConfirmImportResult> ConfirmAsync(
        int uploadId,
        string confirmedBy,
        CancellationToken ct = default)
    {
        var upload = await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct)
            ?? throw new InvalidOperationException($"Upload {uploadId} not found.");

        if (upload.Status == BmUploadStatus.Confirmed)
            throw new InvalidOperationException("This workbook has already been imported.");
        if (upload.Status != BmUploadStatus.Previewed)
            throw new InvalidOperationException(
                "Only a workbook that has been previewed can be imported.");

        var rows = upload.Rows
            .Where(r => r.ImportStatus is BmRowImportStatus.Ok or BmRowImportStatus.Warning)
            .OrderBy(r => r.ExcelRowNo)
            .ToList();

        if (rows.Count == 0)
            throw new InvalidOperationException(
                "There is nothing to import: every row is an error or has been skipped.");

        // The product can be deactivated between preview and confirm, so the lookup is
        // repeated here. Failing the whole confirm is deliberate: importing "most" of a
        // sheet silently would be worse than asking the Admin to skip one row first.
        var codes = rows.Select(r => r.ProductCode).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var products = await _db.Products
            .Where(p => codes.Contains(p.ProductCode))
            .ToDictionaryAsync(p => p.ProductCode, StringComparer.OrdinalIgnoreCase, ct);

        var blockers = rows
            .Where(r => !products.TryGetValue(r.ProductCode, out var p) || !p.IsActive)
            .Select(r => $"row {r.ExcelRowNo} ({r.ProductCode})")
            .ToList();

        if (blockers.Count > 0)
            throw new InvalidOperationException(
                $"Nothing was imported: {string.Join("; ", blockers)} no longer has an active product. " +
                "Fix or skip those rows, then confirm again.");

        // Match existing open jobs by (ProductCode, DueDate) - BM-6 rule.
        // InProgress, Completed, and Cancelled jobs are untouched.
        var openJobs = await _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.Units)
            .Where(j => j.Status == JobStatus.Pending
                     || j.Status == JobStatus.Assigned
                     || j.Status == JobStatus.OnHold)
            .OrderBy(j => j.JobId)
            .ToListAsync(ct);

        var matchedJobIds = new HashSet<int>();
        var refreshedJobs = new List<Job>();
        var newRows = new List<BmRow>();

        foreach (var row in rows)
        {
            var existingOpenJob = openJobs.FirstOrDefault(j =>
                !matchedJobIds.Contains(j.JobId) &&
                string.Equals(j.Product.ProductCode, row.ProductCode, StringComparison.OrdinalIgnoreCase) &&
                j.DueDate == row.DueDate &&
                string.Equals(j.SalesOrder ?? string.Empty, row.SalesOrder ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(j.PONo ?? string.Empty, row.PONo ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (existingOpenJob != null)
            {
                matchedJobIds.Add(existingOpenJob.JobId);

                var prevPriority = existingOpenJob.Priority;
                var prevQuantity = existingOpenJob.Quantity;

                existingOpenJob.Priority = row.Priority;
                existingOpenJob.Quantity = row.Quantity;
                existingOpenJob.SalesOrder = row.SalesOrder;
                existingOpenJob.PONo = row.PONo;
                existingOpenJob.SourceBmRowId = row.RowId;
                existingOpenJob.UpdatedAt = DateTime.UtcNow;

                // Ensure per-unit 40-character tokens are assigned for any additional or missing units
                var currentUnitCount = existingOpenJob.Units.Count;
                if (currentUnitCount < row.Quantity)
                {
                    for (var u = currentUnitCount + 1; u <= row.Quantity; u++)
                    {
                        existingOpenJob.Units.Add(new JobUnit
                        {
                            JobId = existingOpenJob.JobId,
                            UnitIndex = u,
                            TokenNo = TokenGenerator.GenerateProductUnitToken(),
                            Status = UnitAssemblyStatus.Pending,
                            CreatedAt = DateTime.UtcNow
                        });
                    }
                }

                row.ImportStatus = BmRowImportStatus.Imported;
                refreshedJobs.Add(existingOpenJob);

                _db.AuditLogs.Add(new AuditLog
                {
                    OccurredAt = DateTime.UtcNow,
                    UserId = confirmedBy,
                    UserName = confirmedBy,
                    EntityName = nameof(Job),
                    EntityId = existingOpenJob.JobId.ToString(),
                    Action = "ReuploadRefresh",
                    Changes = JsonSerializer.Serialize(new
                    {
                        existingOpenJob.JobNo,
                        UploadId = upload.UploadId,
                        RowId = row.RowId,
                        PreviousPriority = prevPriority,
                        NewPriority = row.Priority,
                        PreviousQuantity = prevQuantity,
                        NewQuantity = row.Quantity,
                        SalesOrder = row.SalesOrder,
                        PONo = row.PONo
                    })
                });
            }
            else
            {
                newRows.Add(row);
            }
        }

        var jobNumbers = await AllocateJobNumbersAsync(newRows.Count, ct);
        var createdJobs = new List<Job>(newRows.Count);

        for (var i = 0; i < newRows.Count; i++)
        {
            var row = newRows[i];

            var job = new Job
            {
                JobNo = jobNumbers[i],
                ProductId = products[row.ProductCode].ProductId,
                // Left unset until the owner answers Q-07 (serial per unit or per job).
                ProductSerial = null,
                SalesOrder = row.SalesOrder,
                PONo = row.PONo,
                Quantity = row.Quantity,
                Priority = row.Priority,
                DueDate = row.DueDate,
                Status = JobStatus.Pending,
                // P4.9 owns how a job gets picked up; until then it waits for an Admin.
                AssignmentMode = AssignmentMode.Manual,
                // PLC is a hook only (Architecture §5.4).
                ExecutionMode = ExecutionMode.Manual,
                SourceBmRowId = row.RowId,
                CreatedAt = DateTime.UtcNow
            };

            // Assign exactly 40-character unique token to each individual unit (1..Quantity)
            for (var u = 1; u <= row.Quantity; u++)
            {
                job.Units.Add(new JobUnit
                {
                    UnitIndex = u,
                    TokenNo = TokenGenerator.GenerateProductUnitToken(),
                    Status = UnitAssemblyStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                });
            }

            createdJobs.Add(job);
            row.ImportStatus = BmRowImportStatus.Imported;
        }

        _db.Jobs.AddRange(createdJobs);
        upload.Status = BmUploadStatus.Confirmed;

        var blackJobs = createdJobs.Where(j => j.Priority == PriorityLevel.Black)
            .Concat(refreshedJobs.Where(j => j.Priority == PriorityLevel.Black))
            .DistinctBy(j => j.JobId)
            .ToList();
        var notified = false;

        if (blackJobs.Count > 0)
        {
            _db.Notifications.Add(_notifications.CreateBlackJobNotice(blackJobs));
            notified = true;
        }

        var absentJobs = openJobs.Where(j => !matchedJobIds.Contains(j.JobId)).ToList();

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = confirmedBy,
            UserName = confirmedBy,
            EntityName = nameof(BmUpload),
            EntityId = upload.UploadId.ToString(),
            Action = "Confirmed",
            Changes = JsonSerializer.Serialize(new
            {
                upload.FileName,
                JobsCreated = createdJobs.Count,
                JobsRefreshed = refreshedJobs.Count,
                AbsentJobs = absentJobs.Count,
                BlackJobs = blackJobs.Count,
                Notified = notified,
                jobNumbers,
                refreshedJobNumbers = refreshedJobs.Select(j => j.JobNo).ToList()
            })
        });

        await _db.SaveChangesAsync(ct);

        return new ConfirmImportResult(
            upload.UploadId,
            createdJobs.Count,
            jobNumbers,
            blackJobs.Count,
            notified,
            refreshedJobs.Count,
            refreshedJobs.Select(j => j.JobNo).ToList(),
            absentJobs.Count);
    }

    public async Task<IReadOnlyList<string>> GetJobNumbersAsync(int uploadId, CancellationToken ct = default)
        => await _db.Jobs
            .Where(j => j.SourceBmRow != null && j.SourceBmRow.UploadId == uploadId)
            .OrderBy(j => j.JobId)
            .Select(j => j.JobNo)
            .ToListAsync(ct);

    public async Task<WorkloadResult> GetWorkloadAsync(
        WorkloadFilter filter,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 200);

        IQueryable<Job> query = _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker);

        if (filter.Priorities.Count > 0)
        {
            var priorities = filter.Priorities.ToList();
            query = query.Where(j => priorities.Contains(j.Priority));
        }

        if (filter.DueFrom is { } from)
            query = query.Where(j => j.DueDate != null && j.DueDate >= from);

        if (filter.DueTo is { } to)
            query = query.Where(j => j.DueDate != null && j.DueDate <= to);

        if (filter.Status is { } status)
            query = query.Where(j => j.Status == status);

        if (filter.UnassignedOnly)
            query = query.Where(j => j.AssignedWorkerId == null);
        else if (!string.IsNullOrWhiteSpace(filter.AssignedWorkerId))
            query = query.Where(j => j.AssignedWorkerId == filter.AssignedWorkerId);

        var total = await query.CountAsync(ct);

        // OrderForWorkboard puts Black first, so pinned jobs lead every page 1.
        var jobs = await _priorities
            .OrderForWorkboard(query)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new WorkloadResult(jobs, total);
    }

    public async Task<IReadOnlyList<ItemWorkloadSummary>> GetWorkloadByItemAsync(
        string? search = null,
        CancellationToken ct = default)
    {
        var query = _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
                .ThenInclude(p => p.Components)
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps)
            .Include(j => j.AssignedWorker)
            .Include(j => j.SourceBmRow)
                .ThenInclude(r => r!.Upload)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(j =>
                (j.Product != null && (j.Product.ProductCode.Contains(s) || j.Product.ProductName.Contains(s))) ||
                j.JobNo.Contains(s));
        }

        var allJobs = await query.ToListAsync(ct);

        var grouped = allJobs
            .GroupBy(j => j.Product?.ProductCode ?? j.ProductSerial ?? $"JOB-{j.JobId}", StringComparer.OrdinalIgnoreCase)
            .Select(g =>
            {
                var firstWithProduct = g.FirstOrDefault(j => j.Product != null);
                var product = firstWithProduct?.Product;
                var orderedJobs = g
                    .OrderBy(j => j.Priority)
                    .ThenBy(j => j.DueDate == null)
                    .ThenBy(j => j.DueDate)
                    .ThenBy(j => j.SourceBmRowId)
                    .ToList();
                var totalQty = g.Sum(j => j.Quantity);
                var completedQty = g.Sum(j => j.QuantityCompleted);
                var highestPriority = g.Any() ? g.Min(j => j.Priority) : PriorityLevel.White;
                var earliestDue = g.Where(j => j.DueDate.HasValue).Select(j => j.DueDate).DefaultIfEmpty(null).Min();

                var totalSteps = product?.Components?.Sum(c => c.Template?.Steps?.Count ?? 0) ?? 0;
                var componentCount = product?.Components?.Count ?? 0;

                var bifurcations = g
                    .GroupBy(j => new { SalesOrder = j.SalesOrder ?? string.Empty, PONo = j.PONo ?? string.Empty })
                    .OrderBy(bg => bg.Key.SalesOrder)
                    .ThenBy(bg => bg.Key.PONo)
                    .Select(bg =>
                    {
                        var bgJobs = bg
                            .OrderBy(j => j.Priority)
                            .ThenBy(j => j.DueDate == null)
                            .ThenBy(j => j.DueDate)
                            .ThenBy(j => j.SourceBmRowId)
                            .Select(j => new ItemJobSummary
                            {
                                JobId = j.JobId,
                                JobNo = j.JobNo,
                                SalesOrder = j.SalesOrder,
                                PONo = j.PONo,
                                Quantity = j.Quantity,
                                QuantityCompleted = j.QuantityCompleted,
                                Priority = j.Priority,
                                DueDate = j.DueDate,
                                Status = j.Status,
                                AssignedWorkerName = j.AssignedWorker?.UserName ?? j.AssignedWorker?.Email,
                                BmFileName = j.SourceBmRow?.Upload?.FileName,
                                BmUploadId = j.SourceBmRow?.UploadId,
                                CreatedAt = j.CreatedAt
                            }).ToList();

                        var req = bg.Sum(j => j.Quantity);
                        var comp = bg.Sum(j => j.QuantityCompleted);
                        var pri = bg.Any() ? bg.Min(j => j.Priority) : PriorityLevel.White;
                        var due = bg.Where(j => j.DueDate.HasValue).Select(j => j.DueDate).DefaultIfEmpty(null).Min();

                        return new ItemOrderBifurcation
                        {
                            SalesOrder = string.IsNullOrEmpty(bg.Key.SalesOrder) ? null : bg.Key.SalesOrder,
                            PONo = string.IsNullOrEmpty(bg.Key.PONo) ? null : bg.Key.PONo,
                            QuantityRequired = req,
                            QuantityCompleted = comp,
                            HighestPriority = pri,
                            EarliestDueDate = due,
                            Jobs = bgJobs
                        };
                    })
                    .ToList();

                return new ItemWorkloadSummary
                {
                    ItemCode = g.Key,
                    ProductId = product?.ProductId,
                    ProductName = product?.ProductName,
                    ProductDescription = product?.Description,
                    ComponentCount = componentCount,
                    TotalSteps = totalSteps,
                    TotalQuantityRequired = totalQty,
                    TotalQuantityCompleted = completedQty,
                    TotalJobs = g.Count(),
                    HighestPriority = highestPriority,
                    EarliestDueDate = earliestDue,
                    Jobs = orderedJobs.Select(j => new ItemJobSummary
                    {
                        JobId = j.JobId,
                        JobNo = j.JobNo,
                        SalesOrder = j.SalesOrder,
                        PONo = j.PONo,
                        Quantity = j.Quantity,
                        QuantityCompleted = j.QuantityCompleted,
                        Priority = j.Priority,
                        DueDate = j.DueDate,
                        Status = j.Status,
                        AssignedWorkerName = j.AssignedWorker?.UserName ?? j.AssignedWorker?.Email,
                        BmFileName = j.SourceBmRow?.Upload?.FileName,
                        BmUploadId = j.SourceBmRow?.UploadId,
                        CreatedAt = j.CreatedAt
                    }).ToList(),
                    OrderBifurcations = bifurcations
                };
            })
            .OrderBy(item => item.HighestPriority)
            .ThenBy(item => item.EarliestDueDate ?? DateOnly.MaxValue)
            .ThenBy(item => item.ItemCode)
            .ToList();

        return grouped;
    }

    public async Task<Job> AssignJobAsync(
        int jobId,
        string? workerId,
        PriorityLevel? overridePriority,
        string changedBy,
        CancellationToken ct = default)
    {
        var job = await _db.Jobs
            .Include(j => j.Product)
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");

        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot reassign a job in status {job.Status}.");

        var oldWorkerId = job.AssignedWorkerId;
        var oldPriority = job.Priority;
        var oldStatus = job.Status;

        if (string.IsNullOrWhiteSpace(workerId))
        {
            job.AssignedWorkerId = null;
            if (job.Status == JobStatus.Assigned)
                job.Status = JobStatus.Pending;
            job.AssignmentMode = AssignmentMode.Manual;
        }
        else
        {
            var worker = await _db.Users.FindAsync([workerId], ct)
                ?? throw new InvalidOperationException($"Worker '{workerId}' not found.");

            job.AssignedWorkerId = worker.Id;
            if (job.Status == JobStatus.Pending)
                job.Status = JobStatus.Assigned;
            job.AssignmentMode = AssignmentMode.Manual;
        }

        if (overridePriority.HasValue && Enum.IsDefined(overridePriority.Value))
        {
            job.Priority = overridePriority.Value;
        }

        job.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = job.JobId.ToString(),
            Action = "Assigned",
            Changes = JsonSerializer.Serialize(new
            {
                job.JobNo,
                PreviousWorkerId = oldWorkerId,
                NewWorkerId = job.AssignedWorkerId,
                PreviousPriority = oldPriority,
                NewPriority = job.Priority,
                PreviousStatus = oldStatus,
                NewStatus = job.Status
            })
        });

        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<int> AutoAssignJobsAsync(
        string changedBy,
        CancellationToken ct = default)
    {
        var workerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Worker", ct);
        if (workerRole == null)
            throw new InvalidOperationException("Worker role not found in system.");

        var workerIds = await _db.UserRoles
            .Where(ur => ur.RoleId == workerRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var workers = await _db.Users
            .Where(u => workerIds.Contains(u.Id) && u.IsActive)
            .ToListAsync(ct);

        if (workers.Count == 0)
            throw new InvalidOperationException("No active workers available for automatic assignment.");

        // Find all open jobs (Pending and Unassigned)
        var openJobsQuery = _db.Jobs
            .Where(j => j.Status == JobStatus.Pending && j.AssignedWorkerId == null);

        var openJobs = await _priorities
            .OrderForWorkboard(openJobsQuery)
            .ToListAsync(ct);

        if (openJobs.Count == 0)
            return 0;

        // Count current active workload per worker (Assigned or InProgress)
        var activeCounts = await _db.Jobs
            .Where(j => j.AssignedWorkerId != null &&
                        (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress))
            .GroupBy(j => j.AssignedWorkerId!)
            .Select(g => new { WorkerId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.WorkerId, x => x.Count, ct);

        // Map each worker to their current job count
        var workerLoads = workers.ToDictionary(w => w.Id, w => activeCounts.GetValueOrDefault(w.Id, 0));

        var assignedCount = 0;
        foreach (var job in openJobs)
        {
            // Pick worker with lowest current workload
            var targetWorkerId = workerLoads.OrderBy(kv => kv.Value).First().Key;

            job.AssignedWorkerId = targetWorkerId;
            job.Status = JobStatus.Assigned;
            job.AssignmentMode = AssignmentMode.Auto;
            job.UpdatedAt = DateTime.UtcNow;

            workerLoads[targetWorkerId]++;
            assignedCount++;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = "Batch",
            Action = "AutoAssign",
            Changes = JsonSerializer.Serialize(new
            {
                AssignedCount = assignedCount,
                AvailableWorkers = workers.Count
            })
        });

        await _db.SaveChangesAsync(ct);
        return assignedCount;
    }

    public async Task<Job> HoldJobAsync(
        int jobId,
        string? reason,
        string changedBy,
        CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([jobId], ct)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");

        if (job.Status is JobStatus.Completed or JobStatus.Cancelled)
            throw new InvalidOperationException($"Cannot hold a job in status {job.Status}.");

        if (job.Status == JobStatus.OnHold)
            return job;

        var prevStatus = job.Status;
        job.Status = JobStatus.OnHold;
        job.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = job.JobId.ToString(),
            Action = "Hold",
            Changes = JsonSerializer.Serialize(new
            {
                job.JobNo,
                PreviousStatus = prevStatus,
                Reason = reason
            })
        });

        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<Job> ResumeJobAsync(
        int jobId,
        string changedBy,
        CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([jobId], ct)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");

        if (job.Status != JobStatus.OnHold)
            throw new InvalidOperationException($"Job {job.JobNo} is not on hold.");

        job.Status = job.QuantityCompleted > 0
            ? JobStatus.InProgress
            : (job.AssignedWorkerId != null ? JobStatus.Assigned : JobStatus.Pending);
        job.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = job.JobId.ToString(),
            Action = "Resume",
            Changes = JsonSerializer.Serialize(new
            {
                job.JobNo,
                ResumedStatus = job.Status
            })
        });

        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<Job> CancelJobAsync(
        int jobId,
        string? reason,
        string changedBy,
        CancellationToken ct = default)
    {
        var job = await _db.Jobs.FindAsync([jobId], ct)
            ?? throw new InvalidOperationException($"Job {jobId} not found.");

        if (job.Status == JobStatus.Completed)
            throw new InvalidOperationException("Cannot cancel a completed job.");

        if (job.Status == JobStatus.Cancelled)
            return job;

        var prevStatus = job.Status;
        job.Status = JobStatus.Cancelled;
        job.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = job.JobId.ToString(),
            Action = "Cancel",
            Changes = JsonSerializer.Serialize(new
            {
                job.JobNo,
                PreviousStatus = prevStatus,
                Reason = reason
            })
        });

        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<Job?> GetJobByIdAsync(int jobId, CancellationToken ct = default)
        => await _db.Jobs
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .Include(j => j.AssignedWorker)
            .Include(j => j.Units.OrderBy(u => u.UnitIndex))
            .Include(j => j.SourceBmRow)
                .ThenInclude(r => r!.Upload)
            .FirstOrDefaultAsync(j => j.JobId == jobId, ct);

    public async Task<Job?> GetJobByNoAsync(string jobNo, CancellationToken ct = default)
        => await _db.Jobs
            .Include(j => j.Product)
                .ThenInclude(p => p.Components.OrderBy(c => c.AssemblyOrder))
                    .ThenInclude(c => c.Template)
                        .ThenInclude(t => t.Steps.OrderBy(s => s.SequenceNo))
            .Include(j => j.AssignedWorker)
            .Include(j => j.Units.OrderBy(u => u.UnitIndex))
            .Include(j => j.SourceBmRow)
                .ThenInclude(r => r!.Upload)
            .FirstOrDefaultAsync(j => j.JobNo == jobNo, ct);

    public async Task<IReadOnlyList<AuditLog>> GetJobAuditLogsAsync(int jobId, CancellationToken ct = default)
        => await _db.AuditLogs
            .Where(a => a.EntityName == nameof(Job) && a.EntityId == jobId.ToString())
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Job>> GetAbsentJobsAsync(int uploadId, CancellationToken ct = default)
    {
        var upload = await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);

        if (upload == null) return [];

        var uploadPairs = upload.Rows
            .Where(r => r.ImportStatus != BmRowImportStatus.Skipped)
            .Select(r => new
            {
                r.ProductCode,
                r.DueDate,
                SalesOrder = r.SalesOrder ?? string.Empty,
                PONo = r.PONo ?? string.Empty
            })
            .ToList();

        var openJobs = await _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Include(j => j.SourceBmRow)
                .ThenInclude(r => r!.Upload)
            .Where(j => j.Status == JobStatus.Pending
                     || j.Status == JobStatus.Assigned
                     || j.Status == JobStatus.OnHold)
            .ToListAsync(ct);

        return openJobs
            .Where(j => !uploadPairs.Any(p =>
                string.Equals(p.ProductCode, j.Product.ProductCode, StringComparison.OrdinalIgnoreCase) &&
                p.DueDate == j.DueDate &&
                string.Equals(p.SalesOrder, j.SalesOrder ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(p.PONo, j.PONo ?? string.Empty, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(j => j.JobNo)
            .ToList();
    }

    public async Task<BmRefreshPreviewResult> GetRefreshPreviewAsync(int uploadId, CancellationToken ct = default)
    {
        var upload = await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct)
            ?? throw new InvalidOperationException($"Upload {uploadId} not found.");

        var openJobs = await _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Where(j => j.Status == JobStatus.Pending
                     || j.Status == JobStatus.Assigned
                     || j.Status == JobStatus.OnHold)
            .OrderBy(j => j.JobId)
            .ToListAsync(ct);

        var matchedRows = new Dictionary<int, BmRowJobMatch>();
        var matchedJobIds = new HashSet<int>();

        var rows = upload.Rows
            .Where(r => r.ImportStatus is BmRowImportStatus.Ok or BmRowImportStatus.Warning)
            .OrderBy(r => r.ExcelRowNo)
            .ToList();

        foreach (var row in rows)
        {
            var match = openJobs.FirstOrDefault(j =>
                !matchedJobIds.Contains(j.JobId) &&
                string.Equals(j.Product.ProductCode, row.ProductCode, StringComparison.OrdinalIgnoreCase) &&
                j.DueDate == row.DueDate &&
                string.Equals(j.SalesOrder ?? string.Empty, row.SalesOrder ?? string.Empty, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(j.PONo ?? string.Empty, row.PONo ?? string.Empty, StringComparison.OrdinalIgnoreCase));

            if (match != null)
            {
                matchedJobIds.Add(match.JobId);
                matchedRows[row.RowId] = new BmRowJobMatch(
                    match.JobId,
                    match.JobNo,
                    match.Priority,
                    row.Priority,
                    match.Quantity,
                    row.Quantity,
                    match.Status,
                    match.AssignedWorker?.UserName);
            }
        }

        var absentJobs = openJobs
            .Where(j => !matchedJobIds.Contains(j.JobId))
            .ToList();

        return new BmRefreshPreviewResult(uploadId, matchedRows, absentJobs);
    }

    // -----------------------------------------------------------------------

    /// <summary>
    /// `JOB-{yyyyMMdd}-{####}`, continuing the day's numbering. The unique index on
    /// <c>JobNo</c> is the backstop: a collision aborts the whole confirm rather than
    /// letting two jobs share a number.
    /// </summary>
    private async Task<List<string>> AllocateJobNumbersAsync(int count, CancellationToken ct)
    {
        var prefix = $"JOB-{DateTime.UtcNow:yyyyMMdd}-";

        var highest = 0;
        var taken = await _db.Jobs
            .Where(j => j.JobNo.StartsWith(prefix))
            .Select(j => j.JobNo)
            .ToListAsync(ct);

        foreach (var jobNo in taken)
        {
            if (int.TryParse(jobNo[prefix.Length..], out var sequence) && sequence > highest)
                highest = sequence;
        }

        var numbers = new List<string>(count);
        for (var i = 0; i < count; i++)
            numbers.Add($"{prefix}{highest + i + 1:0000}");

        return numbers;
    }
}