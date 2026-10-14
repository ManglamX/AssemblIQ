namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Handles saving and deleting component images for templates and products.
/// </summary>
public interface IImageUploadService
{
    /// <summary>
    /// Validates and saves an uploaded image to the default <c>uploads/templates</c> folder.
    /// Returns the relative path stored in DB (e.g. "uploads/templates/abc123.jpg").
    /// Throws <see cref="InvalidOperationException"/> if validation fails.
    /// </summary>
    Task<string> SaveAsync(Stream imageStream, string originalFileName, long fileSizeBytes, CancellationToken ct = default);

    /// <summary>
    /// Validates and saves an uploaded image to <c>uploads/{subfolder}</c>.
    /// Returns the relative path stored in DB (e.g. "uploads/products/abc123.jpg").
    /// Throws <see cref="InvalidOperationException"/> if validation fails.
    /// </summary>
    Task<string> SaveAsync(Stream imageStream, string originalFileName, long fileSizeBytes, string subfolder, CancellationToken ct = default);

    /// <summary>
    /// Deletes an image by its stored relative path. No-ops if path is null/missing.
    /// </summary>
    void Delete(string? relativePath);
}