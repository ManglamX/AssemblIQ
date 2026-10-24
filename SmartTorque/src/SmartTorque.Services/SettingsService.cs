using System.Globalization;
using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

/// <summary>
/// Admin-settings store (Architecture §5.3). Implements ISettingsService and owns
/// the validated application-settings save used by /Settings. Numeric ranges live
/// in the constants below (single source of truth); every successful save writes
/// exactly one AuditLog row with an old→new snapshot of each changed key
/// (Architecture §15 — audit rows are written in batches like AssemblyService).
/// </summary>
public sealed class SettingsService : ISettingsService
{
    public const string KeyDeviceOfflineSeconds = "DeviceOfflineSeconds";
    public const string KeyImageMaxMb = "ImageMaxMb";
    public const string KeyMaxRetriesDefault = "MaxRetriesDefault";
    public const string KeyStableHoldMs = "StableHoldMs";
    public const string KeySessionIdleTimeoutMin = "SessionIdleTimeoutMin";
    public const string KeyBmColourMatchTolerance = "BmColourMatchTolerance";
    public const string KeyRequireManualEntryApproval = "RequireManualEntryApproval";
    public const string KeyBmHeaderMap = "BmHeaderMap";

    public const int DeviceOfflineSecondsMin = 5;
    public const int DeviceOfflineSecondsMax = 600;
    public const int ImageMaxMbMin = 1;
    public const int ImageMaxMbMax = 20;
    public const int MaxRetriesMin = 1;
    public const int MaxRetriesMax = 10;

    private static readonly string[] OverviewKeys =
    [
        KeyDeviceOfflineSeconds,
        KeyImageMaxMb,
        KeyMaxRetriesDefault,
        KeyStableHoldMs,
        KeySessionIdleTimeoutMin,
        KeyBmColourMatchTolerance,
        KeyRequireManualEntryApproval
    ];

    private readonly AppDbContext _db;

    public SettingsService(AppDbContext db) => _db = db;

    /// <summary>Old→new change of one settings key, serialized into the audit row.</summary>
    private sealed record SettingsChange(string Key, string From, string To);

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
        => await _db.Settings
            .AsNoTracking()
            .Where(s => s.Key == key)
            .Select(s => s.Value)
            .FirstOrDefaultAsync(ct);

    public async Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : struct
    {
        var raw = await GetAsync(key, ct);
        if (raw is null) return null;

        try
        {
            if (typeof(T) == typeof(int))
                return (T)(object)int.Parse(raw, CultureInfo.InvariantCulture);
            if (typeof(T) == typeof(bool))
                return (T)(object)bool.Parse(raw);
            if (typeof(T) == typeof(decimal))
                return (T)(object)decimal.Parse(raw, CultureInfo.InvariantCulture);
        }
        catch (FormatException)
        {
            throw new InvalidOperationException(
                $"Setting '{key}' holds value '{raw}', which is not a valid {typeof(T).Name}.");
        }

        throw new NotSupportedException($"Setting type {typeof(T).Name} is not supported.");
    }

    public async Task<IReadOnlyList<SettingOverview>> GetOverviewAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default)
        => await _db.Settings
            .AsNoTracking()
            .Where(s => keys.Contains(s.Key))
            .Select(s => new SettingOverview(s.Key, s.Value, s.Description, s.UpdatedAt, s.UpdatedBy))
            .ToListAsync(ct);

    public async Task SetAsync(string key, string value, string? updatedBy = null, CancellationToken ct = default)
    {
        var row = await _db.Settings
            .FirstOrDefaultAsync(s => s.Key == key, ct);

        if (row is null)
        {
            _db.Settings.Add(new Setting
            {
                Key = key,
                Value = value,
                UpdatedAt = DateTime.UtcNow,
                UpdatedBy = updatedBy
            });
        }
        else if (row.Value != value)
        {
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
        }
        else
        {
            return;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> SaveApplicationSettingsAsync(
        ApplicationSettingsSaveRequest request,
        string? changedBy,
        CancellationToken ct = default)
    {
        ValidateRange(request.DeviceOfflineSeconds, DeviceOfflineSecondsMin, DeviceOfflineSecondsMax,
            $"Device offline threshold must be between {DeviceOfflineSecondsMin} and {DeviceOfflineSecondsMax} seconds.");
        ValidateRange(request.ImageMaxMb, ImageMaxMbMin, ImageMaxMbMax,
            $"Image size limit must be between {ImageMaxMbMin} and {ImageMaxMbMax} MB.");
        ValidateRange(request.MaxRetriesDefault, MaxRetriesMin, MaxRetriesMax,
            $"Default max retries must be between {MaxRetriesMin} and {MaxRetriesMax}.");

        var headerMapReset = string.IsNullOrWhiteSpace(request.BmHeaderMap);
        if (!headerMapReset)
            ValidateHeaderMap(request.BmHeaderMap!);

        var wanted = new List<string>
        {
            KeyDeviceOfflineSeconds,
            KeyImageMaxMb,
            KeyMaxRetriesDefault,
            BmSettings.HeaderMapKey
        };

        var rows = await _db.Settings
            .Where(s => wanted.Contains(s.Key))
            .ToListAsync(ct);

        var changes = new List<SettingsChange>();

        var numeric = new (string Key, string Value, string Description)[]
        {
            (KeyDeviceOfflineSeconds,
                request.DeviceOfflineSeconds.ToString(CultureInfo.InvariantCulture),
                "Device considered offline after this many seconds without heartbeats."),
            (KeyImageMaxMb,
                request.ImageMaxMb.ToString(CultureInfo.InvariantCulture),
                "Maximum upload size for template and product images, in MB."),
            (KeyMaxRetriesDefault,
                request.MaxRetriesDefault.ToString(CultureInfo.InvariantCulture),
                "Default max retries pre-filled for newly created template steps.")
        };

        foreach (var (key, value, description) in numeric)
        {
            var change = Upsert(rows, key, value, changedBy, description);
            if (change is not null) changes.Add(change);
        }

        if (headerMapReset)
        {
            var headerMapRow = rows.FirstOrDefault(r => r.Key == BmSettings.HeaderMapKey);
            if (headerMapRow is not null)
            {
                _db.Settings.Remove(headerMapRow);
                changes.Add(new SettingsChange(BmSettings.HeaderMapKey, headerMapRow.Value, "(default)"));
            }
        }
        else
        {
            var change = Upsert(
                rows,
                BmSettings.HeaderMapKey,
                request.BmHeaderMap!.Trim(),
                changedBy,
                "Column headings to look for in Supplier BM workbooks (JSON).");
            if (change is not null) changes.Add(change);
        }

        if (changes.Count == 0) return 0;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Setting),
            EntityId = null,
            Action = "Updated",
            Changes = JsonSerializer.Serialize(changes)
        });

        await _db.SaveChangesAsync(ct);
        return changes.Count;
    }

    /// <summary>Updates or inserts one row with its tracked change, or null when the value is unchanged.</summary>
    private SettingsChange? Upsert(
        List<Setting> rows,
        string key,
        string newValue,
        string? changedBy,
        string descriptionOnInsert)
    {
        var row = rows.FirstOrDefault(r => r.Key == key);

        var change = new SettingsChange(key, row is null ? "(default)" : row.Value, newValue);
        if (row is not null && change.From == newValue) return null;

        if (row is null)
        {
            row = new Setting { Key = key, Description = descriptionOnInsert };
            rows.Add(row);
            _db.Settings.Add(row);
        }

        row.Value = newValue;
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = changedBy;
        return change;
    }

    private static void ValidateRange(int value, int min, int max, string message)
    {
        if (value < min || value > max)
            throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Keys must all be present and non-empty (case-sensitive, exactly as
    /// BmImportService deserializes them). Extra properties are tolerated,
    /// mirroring the importer's own leniency.
    /// </summary>
    private static void ValidateHeaderMap(string json)
    {
        BmHeaderMap? map;
        try
        {
            map = JsonSerializer.Deserialize<BmHeaderMap>(json);
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                "BM column headings must be a valid JSON object, e.g. {\"ProductCode\":\"Item Code\"}.");
        }

        if (map is null)
            throw new InvalidOperationException("BM column headings must be a JSON object.");

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(map.ProductCode)) missing.Add("ProductCode");
        if (string.IsNullOrWhiteSpace(map.Quantity)) missing.Add("Quantity");
        if (string.IsNullOrWhiteSpace(map.DueDate)) missing.Add("DueDate");
        if (string.IsNullOrWhiteSpace(map.Remarks)) missing.Add("Remarks");
        if (string.IsNullOrWhiteSpace(map.SalesOrder)) missing.Add("SalesOrder");
        if (string.IsNullOrWhiteSpace(map.PONo)) missing.Add("PONo");

        if (missing.Count > 0)
            throw new InvalidOperationException(
                $"BM column heading keys must not be empty: {string.Join(", ", missing)}.");
    }
}