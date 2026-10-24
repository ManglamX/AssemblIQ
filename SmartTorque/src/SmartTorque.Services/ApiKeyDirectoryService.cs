using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;

namespace SmartTorque.Services;

public interface IApiKeyDirectoryService
{
    Task<bool> IsMainAdminAsync(string userId, CancellationToken ct = default);

    // Main Admin Operations
    Task<IReadOnlyList<ApiKeyDirectoryEntry>> GetAllKeysForMainAdminAsync(CancellationToken ct = default);
    Task<ApiKeyDirectoryEntry> CreateKeyAsync(string name, string scope, string description, string? rawApiKey, string createdBy, CancellationToken ct = default);
    Task<bool> ToggleKeyStatusAsync(int apiKeyId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyAccessGrant>> GetPendingRequestsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyAccessGrant>> GetAllGrantsAsync(CancellationToken ct = default);
    Task<ApiKeyAccessGrant> ApproveRequestAsync(int grantId, string decidedByUserId, CancellationToken ct = default);
    Task<ApiKeyAccessGrant> RejectRequestAsync(int grantId, string decidedByUserId, string? rejectionReason, CancellationToken ct = default);
    Task<bool> RevokeAccessAsync(int grantId, string decidedByUserId, CancellationToken ct = default);

    // Regular Admin Operations
    Task<IReadOnlyList<ApiKeyDirectoryEntry>> GetAvailableKeysForUserAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyAccessGrant>> GetApprovedGrantsForUserAsync(string userId, CancellationToken ct = default);
    Task<IReadOnlyList<ApiKeyAccessGrant>> GetRequestsForUserAsync(string userId, CancellationToken ct = default);
    Task<ApiKeyAccessGrant> RequestAccessAsync(int apiKeyId, string userId, string? reason, CancellationToken ct = default);
}

public class ApiKeyDirectoryService : IApiKeyDirectoryService
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApiKeyDirectoryService(AppDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    public async Task<bool> IsMainAdminAsync(string userId, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        return user.IsMainAdmin || string.Equals(user.Email, "admin@smarttorque.local", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<ApiKeyDirectoryEntry>> GetAllKeysForMainAdminAsync(CancellationToken ct = default)
    {
        return await _db.ApiKeyDirectoryEntries
            .Include(k => k.Grants)
                .ThenInclude(g => g.User)
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);
    }

    public async Task<ApiKeyDirectoryEntry> CreateKeyAsync(
        string name,
        string scope,
        string description,
        string? rawApiKey,
        string createdBy,
        CancellationToken ct = default)
    {
        var keyToSave = string.IsNullOrWhiteSpace(rawApiKey)
            ? TokenGenerator.GenerateApiKey("st_api_")
            : rawApiKey.Trim();

        var sha256 = SHA256.HashData(Encoding.UTF8.GetBytes(keyToSave));
        var keyHash = Convert.ToHexString(sha256).ToLowerInvariant();

        var entry = new ApiKeyDirectoryEntry
        {
            KeyName = name.Trim(),
            ApiScope = scope.Trim(),
            Description = description.Trim(),
            RawApiKey = keyToSave,
            ApiKeyHash = keyHash,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.ApiKeyDirectoryEntries.Add(entry);
        await _db.SaveChangesAsync(ct);
        return entry;
    }

    public async Task<bool> ToggleKeyStatusAsync(int apiKeyId, CancellationToken ct = default)
    {
        var key = await _db.ApiKeyDirectoryEntries.FindAsync([apiKeyId], ct);
        if (key == null) return false;

        key.IsActive = !key.IsActive;
        await _db.SaveChangesAsync(ct);
        return key.IsActive;
    }

    public async Task<IReadOnlyList<ApiKeyAccessGrant>> GetPendingRequestsAsync(CancellationToken ct = default)
    {
        return await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Include(g => g.User)
            .Where(g => g.Status == ApiKeyRequestStatus.Pending)
            .OrderBy(g => g.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKeyAccessGrant>> GetAllGrantsAsync(CancellationToken ct = default)
    {
        return await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Include(g => g.User)
            .Include(g => g.DecidedByUser)
            .OrderByDescending(g => g.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<ApiKeyAccessGrant> ApproveRequestAsync(int grantId, string decidedByUserId, CancellationToken ct = default)
    {
        var grant = await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.GrantId == grantId, ct)
            ?? throw new InvalidOperationException($"Access request {grantId} not found.");

        grant.Status = ApiKeyRequestStatus.Approved;
        grant.DecidedAt = DateTime.UtcNow;
        grant.DecidedByUserId = decidedByUserId;
        grant.RejectionReason = null;

        await _db.SaveChangesAsync(ct);
        return grant;
    }

    public async Task<ApiKeyAccessGrant> RejectRequestAsync(
        int grantId,
        string decidedByUserId,
        string? rejectionReason,
        CancellationToken ct = default)
    {
        var grant = await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Include(g => g.User)
            .FirstOrDefaultAsync(g => g.GrantId == grantId, ct)
            ?? throw new InvalidOperationException($"Access request {grantId} not found.");

        grant.Status = ApiKeyRequestStatus.Rejected;
        grant.DecidedAt = DateTime.UtcNow;
        grant.DecidedByUserId = decidedByUserId;
        grant.RejectionReason = rejectionReason;

        await _db.SaveChangesAsync(ct);
        return grant;
    }

    public async Task<bool> RevokeAccessAsync(int grantId, string decidedByUserId, CancellationToken ct = default)
    {
        var grant = await _db.ApiKeyAccessGrants.FindAsync([grantId], ct);
        if (grant == null) return false;

        grant.Status = ApiKeyRequestStatus.Revoked;
        grant.DecidedAt = DateTime.UtcNow;
        grant.DecidedByUserId = decidedByUserId;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<ApiKeyDirectoryEntry>> GetAvailableKeysForUserAsync(string userId, CancellationToken ct = default)
    {
        // Return active keys in the directory so the Admin can choose one to request
        return await _db.ApiKeyDirectoryEntries
            .Where(k => k.IsActive)
            .OrderBy(k => k.KeyName)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKeyAccessGrant>> GetApprovedGrantsForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Where(g => g.UserId == userId && g.Status == ApiKeyRequestStatus.Approved && g.ApiKey.IsActive)
            .OrderByDescending(g => g.DecidedAt)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ApiKeyAccessGrant>> GetRequestsForUserAsync(string userId, CancellationToken ct = default)
    {
        return await _db.ApiKeyAccessGrants
            .Include(g => g.ApiKey)
            .Include(g => g.DecidedByUser)
            .Where(g => g.UserId == userId)
            .OrderByDescending(g => g.RequestedAt)
            .ToListAsync(ct);
    }

    public async Task<ApiKeyAccessGrant> RequestAccessAsync(
        int apiKeyId,
        string userId,
        string? reason,
        CancellationToken ct = default)
    {
        var key = await _db.ApiKeyDirectoryEntries.FindAsync([apiKeyId], ct)
            ?? throw new InvalidOperationException($"API key {apiKeyId} does not exist.");

        var existingGrant = await _db.ApiKeyAccessGrants
            .FirstOrDefaultAsync(g => g.ApiKeyId == apiKeyId && g.UserId == userId, ct);

        if (existingGrant != null)
        {
            if (existingGrant.Status == ApiKeyRequestStatus.Approved)
            {
                return existingGrant;
            }

            // Reset to pending if rejected or revoked
            existingGrant.Status = ApiKeyRequestStatus.Pending;
            existingGrant.RequestReason = reason;
            existingGrant.RequestedAt = DateTime.UtcNow;
            existingGrant.DecidedAt = null;
            existingGrant.DecidedByUserId = null;
            existingGrant.RejectionReason = null;

            await _db.SaveChangesAsync(ct);
            return existingGrant;
        }

        var grant = new ApiKeyAccessGrant
        {
            ApiKeyId = apiKeyId,
            UserId = userId,
            RequestReason = reason,
            Status = ApiKeyRequestStatus.Pending,
            RequestedAt = DateTime.UtcNow
        };

        _db.ApiKeyAccessGrants.Add(grant);
        await _db.SaveChangesAsync(ct);
        return grant;
    }
}