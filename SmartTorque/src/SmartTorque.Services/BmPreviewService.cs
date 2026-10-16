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
        => await _db.BmUploads
            .Include(u => u.Rows)
            .FirstOrDefaultAsync(u => u.UploadId == uploadId, ct);

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
        var status = await _db.BmUploads
            .Where(u => u.UploadId == uploadId)
            .Select(u => u.Status)
            .FirstOrDefaultAsync(ct);

        if (status == BmUploadStatus.Confirmed)
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