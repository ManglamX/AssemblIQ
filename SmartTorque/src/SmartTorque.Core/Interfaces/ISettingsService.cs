namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Reads and writes the <see cref="Entities.Setting"/> key/value store.
/// </summary>
public interface ISettingsService
{
    Task<string?> GetAsync(string key, CancellationToken ct = default);
    Task<T?> GetAsync<T>(string key, CancellationToken ct = default) where T : struct;
    Task SetAsync(string key, string value, string? updatedBy = null, CancellationToken ct = default);
}