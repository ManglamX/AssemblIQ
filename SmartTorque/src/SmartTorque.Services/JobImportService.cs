using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>Outcome of confirming a BM preview (Architecture §7.3 step 5).</summary>
public record ConfirmImportResult(
    int UploadId,
    int JobsCreated,
    IReadOnlyList<string> JobNumbers,
    int BlackJobs,
    bool NotificationRaised);

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

        var jobNumbers = await AllocateJobNumbersAsync(rows.Count, ct);
        var jobs = new List<Job>(rows.Count);

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];

            jobs.Add(new Job
            {
                JobNo = jobNumbers[i],
                ProductId = products[row.ProductCode].ProductId,
                // Left unset until the owner answers Q-07 (serial per unit or per job).
                ProductSerial = null,
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
            });

            row.ImportStatus = BmRowImportStatus.Imported;
        }

        _db.Jobs.AddRange(jobs);
        upload.Status = BmUploadStatus.Confirmed;

        var blackJobs = jobs.Where(j => j.Priority == PriorityLevel.Black).ToList();
        var notified = false;

        if (blackJobs.Count > 0)
        {
            _db.Notifications.Add(_notifications.CreateBlackJobNotice(blackJobs));
            notified = true;
        }

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
                JobsCreated = jobs.Count,
                BlackJobs = blackJobs.Count,
                Notified = notified,
                jobNumbers
            })
        });

        await _db.SaveChangesAsync(ct);

        return new ConfirmImportResult(
            upload.UploadId, jobs.Count, jobNumbers, blackJobs.Count, notified);
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