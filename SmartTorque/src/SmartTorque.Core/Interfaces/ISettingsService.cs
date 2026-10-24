namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Reads and writes the <see cref="Entities.Setting"/> key/value store.
/// </summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : struct;

    /// <summary>Rows for the requested keys. Missing keys are omitted.</summary>
    Task<IReadOnlyList<SettingOverview>> GetOverviewAsync(
        IReadOnlyCollection<string> keys, CancellationToken ct = default);

    Task SetAsync(string key, string value, string? updatedBy = null, CancellationToken ct = default);

    /// <summary>
    /// Saves the validated /Settings form (device thresholds, image limit, template
    /// defaults, BM header map). Returns the number of keys actually changed.
    /// Throws <see cref="InvalidOperationException"/> when a value is out of range —
    /// nothing is persisted in that case.
    /// </summary>
    Task<int> SaveApplicationSettingsAsync(
        ApplicationSettingsSaveRequest request,
        string? changedBy,
        CancellationToken ct = default);
}

/// <summary>A settings row with its metadata, for admin views.</summary>
public record SettingOverview(
    string Key,
    string Value,
    string? Description,
    DateTime UpdatedAt,
    string? UpdatedBy);

/// <summary>
/// Editable application settings for /Settings. Keys not listed there
/// (device-side or Phase 8 planned) cannot be changed through this request.
/// </summary>
public record ApplicationSettingsSaveRequest(
    int DeviceOfflineSeconds,
    int ImageMaxMb,
    int MaxRetriesDefault,
    string? BmHeaderMap);