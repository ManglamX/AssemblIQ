using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Holds an uploaded BM workbook as a preview (PRD BM-7, BM-8; Architecture §7.3).
/// Nothing becomes a Job until the Admin confirms the import (P4.6).
/// </summary>
public interface IBmPreviewService
{
    /// <summary>Saves the workbook, parses it and persists an upload with its rows.</summary>
    Task<BmUpload> UploadAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        string uploadedBy,
        CancellationToken ct = default);

    Task<BmUpload> UploadAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        string uploadedBy,
        BmHeaderMap? customMap,
        CancellationToken ct = default);

    Task<BmUpload?> GetAsync(int uploadId, CancellationToken ct = default);

    /// <summary>Re-evaluates rows against current database state (products created, jobs generated).</summary>
    Task ReevaluateRowsAsync(BmUpload upload, CancellationToken ct = default);

    Task<List<BmUpload>> RecentAsync(int count = 10, CancellationToken ct = default);

    /// <summary>
    /// File-level failures store no text, so a failed upload is re-read to recover them.
    /// </summary>
    Task<List<string>> ReadFileErrorsAsync(BmUpload upload, CancellationToken ct = default);

    /// <summary>Per-row priority override (BM-5).</summary>
    Task SetRowPriorityAsync(int rowId, PriorityLevel priority, CancellationToken ct = default);

    /// <summary>Exclude a row from the import, or put it back.</summary>
    Task SetRowSkippedAsync(int rowId, bool skipped, CancellationToken ct = default);

    /// <summary>Abandon a preview: drops the rows, the upload and the stored file.</summary>
    Task CancelAsync(int uploadId, CancellationToken ct = default);
}

public class BmPreviewService : IBmPreviewService
{
    private readonly AppDbContext _db;
    private readonly IBmImportService _import;
    private readonly IBmFileStore _files;

    public BmPreviewService(AppDbContext db, IBmImportService import, IBmFileStore files)
    {
        _db = db;
        _import = import;
        _files = files;
    }

    public Task<BmUpload> UploadAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        string uploadedBy,
        CancellationToken ct = default)
        => UploadAsync(stream, fileName, fileSizeBytes, uploadedBy, null, ct);

    public async Task<BmUpload> UploadAsync(
        Stream stream,
        string fileName,
        long fileSizeBytes,
        string uploadedBy,
        BmHeaderMap? customMap,
        CancellationToken ct = default)
    {
        // Store first so a rejected file never leaves a row pointing at nothing.
        var storedPath = await _files.SaveAsync(stream, fileName, fileSizeBytes, ct);

        BmParseResult parsed;
        await using (var stored = _files.OpenRead(storedPath))
        {
            parsed = stored == null
                ? new BmParseResult(fileName, null, customMap ?? BmHeaderMap.Default, [],
                    ["The uploaded file could not be read back."])
                : await _import.ParseAsync(stored, fileName, customMap, ct);
        }

        var upload = new BmUpload
        {
            FileName = fileName,
            StoredPath = storedPath,
            UploadedBy = uploadedBy,
            UploadedAt = DateTime.UtcNow,
            Status = parsed.HasFatalError ? BmUploadStatus.Failed : BmUploadStatus.Previewed,
            RowCount = parsed.Rows.Count,
            ErrorCount = parsed.ErrorCount
        };

        foreach (var row in parsed.Rows)
        {
            upload.Rows.Add(new BmRow
            {
                ExcelRowNo = row.ExcelRowNo,
                ProductCode = row.ProductCode,
                Quantity = row.Quantity ?? 0,
                DueDate = row.DueDate,
                Remarks = row.Remarks,
                ColourHex = row.ColourHex,
                Priority = row.Priority,
                ImportStatus = row.Status,
                ImportMessage = row.Message,
                SalesOrder = row.SalesOrder,
                PONo = row.PONo
            });
        }

        _db.BmUploads.Add(upload);
        await _db.SaveChangesAsync(ct);

        return upload;
    }

    public async Task<BmUpload?> GetAsync(int uploadId, CancellationToken ct = default)
    {
        var upload = await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);

        if (upload != null)
        {
            await ReevaluateRowsAsync(upload, ct);
        }

        return upload;
    }

    public async Task ReevaluateRowsAsync(BmUpload upload, CancellationToken ct = default)
    {
        if (upload.Rows == null || upload.Rows.Count == 0) return;

        var codes = upload.Rows
            .Where(r => !string.IsNullOrWhiteSpace(r.ProductCode))
            .Select(r => r.ProductCode.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        if (codes.Count == 0) return;

        // 1. Resolve upload's department from uploader first
        int? uploadDeptId = null;
        string? uploadDeptName = null;
        if (!string.IsNullOrEmpty(upload.UploadedBy))
        {
            var uploader = await _db.Users.FirstOrDefaultAsync(u => u.UserName == upload.UploadedBy || u.Email == upload.UploadedBy, ct);
            if (uploader?.DepartmentId != null)
            {
                uploadDeptId = uploader.DepartmentId;
                var dept = await _db.Departments.FindAsync([uploadDeptId.Value], ct);
                uploadDeptName = dept?.Name;
            }
        }

        var products = await _db.Products
            .Include(p => p.Department)
            .Where(p => codes.Contains(p.ProductCode.ToUpper()))
            .ToDictionaryAsync(p => p.ProductCode.Trim().ToUpperInvariant(), ct);

        // If uploader had no department, check if all products belong to a single department
        if (!uploadDeptId.HasValue)
        {
            uploadDeptId = products.Values.FirstOrDefault(p => p.DepartmentId.HasValue)?.DepartmentId;
            if (uploadDeptId.HasValue)
            {
                var dept = await _db.Departments.FindAsync([uploadDeptId.Value], ct);
                uploadDeptName = dept?.Name;
            }
        }

        var rowIds = upload.Rows.Select(r => r.RowId).ToList();
        var existingJobsQuery = _db.Jobs
            .Include(j => j.Product)
            .Include(j => j.AssemblyLine)
            .Where(j => (j.SourceBmRowId != null && rowIds.Contains(j.SourceBmRowId.Value)) ||
                        (!string.IsNullOrWhiteSpace(j.SalesOrder) && codes.Contains(j.Product.ProductCode.ToUpper())));

        if (uploadDeptId.HasValue)
        {
            existingJobsQuery = existingJobsQuery.Where(j =>
                j.Product != null && j.Product.DepartmentId == uploadDeptId.Value);
        }

        var existingJobs = await existingJobsQuery.ToListAsync(ct);

        bool hasChanges = false;

        foreach (var row in upload.Rows)
        {
            var code = (row.ProductCode ?? string.Empty).Trim().ToUpperInvariant();
            var so = (row.SalesOrder ?? string.Empty).Trim().ToUpperInvariant();
            var po = (row.PONo ?? string.Empty).Trim().ToUpperInvariant();
            bool hasOrderIds = so.Length > 0 || po.Length > 0;

            // 1. Strict Department Ownership Check:
            // An item code belongs to only one department. If this product belongs to another department,
            // it can NEVER be imported or owned by this department.
            if (products.TryGetValue(code, out var prod) &&
                uploadDeptId.HasValue && prod.DepartmentId.HasValue && prod.DepartmentId.Value != uploadDeptId.Value)
            {
                var foreignDept = prod.Department?.Name ?? "another department";
                var msg = $"Item code '{prod.ProductCode}' belongs to {foreignDept}. It cannot be imported by {uploadDeptName ?? "this department"}.";
                if (row.ImportStatus != BmRowImportStatus.Error || row.ImportMessage != msg)
                {
                    row.ImportStatus = BmRowImportStatus.Error;
                    row.ImportMessage = msg;
                    hasChanges = true;
                }
                continue;
            }

            // 2. Check if a Job was already created for this row IN THIS DEPARTMENT
            Job? matchedJob = null;
            if (row.RowId > 0)
            {
                matchedJob = existingJobs.FirstOrDefault(j => j.SourceBmRowId == row.RowId);
            }
            if (matchedJob == null && hasOrderIds)
            {
                matchedJob = existingJobs.FirstOrDefault(j =>
                    string.Equals(j.Product?.ProductCode?.Trim(), code, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((j.SalesOrder ?? string.Empty).Trim(), so, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals((j.PONo ?? string.Empty).Trim(), po, StringComparison.OrdinalIgnoreCase));
            }
            if (matchedJob == null && !hasOrderIds && row.DueDate.HasValue)
            {
                matchedJob = existingJobs.FirstOrDefault(j =>
                    string.Equals(j.Product?.ProductCode?.Trim(), code, StringComparison.OrdinalIgnoreCase) &&
                    j.DueDate == row.DueDate &&
                    string.IsNullOrWhiteSpace(j.SalesOrder) &&
                    string.IsNullOrWhiteSpace(j.PONo));
            }

            if (matchedJob != null)
            {
                // Order is ALREADY created in this department! Mark Imported so it's not created again.
                if (row.ImportStatus != BmRowImportStatus.Imported)
                {
                    row.ImportStatus = BmRowImportStatus.Imported;
                    row.ImportMessage = null;
                    hasChanges = true;
                }
            }
            else
            {
                // No job created in this department
                if (prod != null)
                {
                    if (prod.IsActive)
                    {
                        if (row.ImportStatus is BmRowImportStatus.Error or BmRowImportStatus.Imported)
                        {
                            if (row.Quantity > 0)
                            {
                                row.ImportStatus = BmRowImportStatus.Ok;
                                row.ImportMessage = null;
                                hasChanges = true;
                            }
                        }
                    }
                    else
                    {
                        if (row.ImportStatus != BmRowImportStatus.Error)
                        {
                            row.ImportStatus = BmRowImportStatus.Error;
                            row.ImportMessage = $"Product '{row.ProductCode}' is not active.";
                            hasChanges = true;
                        }
                    }
                }
                else
                {
                    // Product not found in database
                    if (row.ImportStatus != BmRowImportStatus.Error && row.ImportStatus != BmRowImportStatus.Skipped)
                    {
                        row.ImportStatus = BmRowImportStatus.Error;
                        row.ImportMessage = $"Product '{row.ProductCode}' was not found. Please create this product first.";
                        hasChanges = true;
                    }
                }
            }
        }

        if (hasChanges)
        {
            upload.ErrorCount = upload.Rows.Count(r => r.ImportStatus == BmRowImportStatus.Error);
            if (upload.Status == BmUploadStatus.Confirmed && upload.Rows.Any(r => r.ImportStatus is BmRowImportStatus.Ok or BmRowImportStatus.Warning))
            {
                upload.Status = BmUploadStatus.Previewed;
            }
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task<List<BmUpload>> RecentAsync(int count = 10, CancellationToken ct = default)
        => await _db.BmUploads
            .OrderByDescending(u => u.UploadedAt)
            .Take(count)
            .ToListAsync(ct);

    public async Task<List<string>> ReadFileErrorsAsync(BmUpload upload, CancellationToken ct = default)
    {
        if (upload.Status != BmUploadStatus.Failed) return [];

        await using var stored = _files.OpenRead(upload.StoredPath);
        if (stored == null) return ["The uploaded file is no longer available."];

        var parsed = await _import.ParseAsync(stored, upload.FileName, ct);
        return parsed.FileErrors;
    }

    public async Task SetRowPriorityAsync(int rowId, PriorityLevel priority, CancellationToken ct = default)
    {
        if (priority < PriorityLevel.Black || priority > PriorityLevel.White)
            throw new InvalidOperationException("Priority must be 1 (Black) to 6 (White).");

        var row = await GetRowAsync(rowId, ct);
        await EnsureEditableAsync(row.UploadId, ct);

        row.Priority = priority;
        await _db.SaveChangesAsync(ct);
    }

    public async Task SetRowSkippedAsync(int rowId, bool skipped, CancellationToken ct = default)
    {
        var row = await GetRowAsync(rowId, ct);
        await EnsureEditableAsync(row.UploadId, ct);

        if (skipped)
        {
            if (row.ImportStatus is not (BmRowImportStatus.Ok or BmRowImportStatus.Warning))
                throw new InvalidOperationException("Only rows that can be imported can be skipped.");

            row.ImportStatus = BmRowImportStatus.Skipped;
        }
        else if (row.ImportStatus == BmRowImportStatus.Skipped)
        {
            // Skipped is the only reversible status, so restore by re-checking the
            // single condition that produced a warning. Error rows cannot be skipped,
            // so they can never reappear here.
            row.ImportStatus = await IsUnrecognisedColourAsync(row, ct)
                ? BmRowImportStatus.Warning
                : BmRowImportStatus.Ok;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task CancelAsync(int uploadId, CancellationToken ct = default)
    {
        var upload = await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct)
            ?? throw new InvalidOperationException($"Upload {uploadId} not found.");

        if (upload.Status == BmUploadStatus.Confirmed)
            throw new InvalidOperationException("An upload that has been imported cannot be cancelled.");

        // Rows go with it (cascade); the stored file is removed separately.
        _db.BmUploads.Remove(upload);
        await _db.SaveChangesAsync(ct);

        _files.Delete(upload.StoredPath);
    }

    // -----------------------------------------------------------------------

    private async Task<BmRow> GetRowAsync(int rowId, CancellationToken ct)
        => await _db.BmRows.FirstOrDefaultAsync(r => r.RowId == rowId, ct)
            ?? throw new InvalidOperationException($"Row {rowId} not found.");

    private async Task EnsureEditableAsync(int uploadId, CancellationToken ct)
    {
        var upload = await _db.BmUploads
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);

        if (upload?.Status == BmUploadStatus.Confirmed)
            throw new InvalidOperationException(
                "This upload has already been imported and can no longer be changed.");
    }

    private async Task<bool> IsUnrecognisedColourAsync(BmRow row, CancellationToken ct)
    {
        var map = await _db.PriorityColourMap
            .Where(m => m.IsActive)
            .ToListAsync(ct);

        return !ColourMatcher.Resolve(row.ColourHex, map).WithinTolerance;
    }
}