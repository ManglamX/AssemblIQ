using System.Text.Json;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;

namespace SmartTorque.Services;

public class AssemblyService : IAssemblyService
{
    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IPriorityService _priorities;

    public AssemblyService(AppDbContext db, INotificationService notifications, IPriorityService priorities)
    {
        _db = db;
        _notifications = notifications;
        _priorities = priorities;
    }

    #region Assembly Lines

    public async Task<IReadOnlyList<AssemblyLineDto>> GetAssemblyLinesAsync(
        string? search = null,
        string? department = null,
        bool? isActive = null,
        CancellationToken ct = default)
    {
        IQueryable<AssemblyLine> query = _db.AssemblyLines
            .AsNoTracking()
            .Include(a => a.ProductMappings)
            .Include(a => a.AssignedWorkers)
            .Include(a => a.Jobs);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(a => a.AssemblyCode.Contains(s) || a.AssemblyName.Contains(s) || a.Department.Contains(s));
        }

        if (!string.IsNullOrWhiteSpace(department))
        {
            var dept = department.Trim();
            query = query.Where(a => a.Department == dept);
        }

        if (isActive.HasValue)
        {
            query = query.Where(a => a.IsActive == isActive.Value && (a.DepartmentNavigation == null || a.DepartmentNavigation.IsActive));
        }

        var lines = await query.OrderBy(a => a.AssemblyCode).ToListAsync(ct);

        return lines.Select(a =>
        {
            var activeWorkersCount = a.AssignedWorkers.Count(w => w.IsActive);
            var pendingTasks = a.Jobs.Count(j => j.Status == JobStatus.Pending || (j.Status == JobStatus.Assigned && j.AssignedWorkerId == null));
            var inProgressTasks = a.Jobs.Count(j => j.Status == JobStatus.InProgress || (j.Status == JobStatus.Assigned && j.AssignedWorkerId != null));
            var activeWorkload = a.Jobs
                .Where(j => j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress)
                .Sum(j => j.EstimatedWorkload);

            return new AssemblyLineDto(
                a.AssemblyLineId,
                a.AssemblyCode,
                a.AssemblyName,
                a.Department,
                a.Description,
                a.IsActive,
                a.ProductMappings.Count,
                activeWorkersCount,
                pendingTasks,
                inProgressTasks,
                activeWorkload,
                a.CreatedAt);
        }).ToList();
    }

    public async Task<AssemblyLineDetailDto?> GetAssemblyLineDetailAsync(int assemblyLineId, CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines
            .AsNoTracking()
            .Include(a => a.ProductMappings)
                .ThenInclude(m => m.Product)
            .Include(a => a.AssignedWorkers)
            .Include(a => a.Jobs)
                .ThenInclude(j => j.Product)
            .Include(a => a.Jobs)
                .ThenInclude(j => j.AssignedWorker)
            .FirstOrDefaultAsync(a => a.AssemblyLineId == assemblyLineId, ct);

        if (line == null)
            return null;

        var products = line.ProductMappings
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.Product.ProductCode)
            .Select(m => new ProductMappingDto(
                m.ProductAssemblyMappingId,
                m.ProductId,
                m.Product.ProductCode,
                m.Product.ProductName,
                m.Product.Description,
                m.IsDefault,
                m.Product.IsActive,
                m.CreatedAt))
            .ToList();

        var activeJobs = line.Jobs
            .Where(j => j.Status == JobStatus.Pending || j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress)
            .OrderBy(j => j.Priority)
            .ThenBy(j => j.DueDate)
            .Select(j => new AssemblyTaskDto(
                j.JobId,
                j.JobNo,
                j.ProductId,
                j.Product?.ProductCode ?? string.Empty,
                j.Product?.ProductName ?? string.Empty,
                j.AssemblyLineId,
                line.AssemblyCode,
                j.AssignedWorkerId,
                j.AssignedWorker?.FullName ?? j.AssignedWorker?.UserName,
                j.Status,
                j.Priority,
                j.EstimatedWorkload,
                j.Quantity,
                j.QuantityCompleted,
                j.DueDate,
                j.AssignedAt,
                j.CreatedAt))
            .ToList();

        // Calculate worker stats for workers assigned to this line
        var workersList = new List<WorkerLineDto>();
        foreach (var w in line.AssignedWorkers.OrderBy(w => w.FullName))
        {
            var workerJobs = line.Jobs.Where(j => j.AssignedWorkerId == w.Id && (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress)).ToList();
            var workload = workerJobs.Sum(j => j.EstimatedWorkload);
            var taskCount = workerJobs.Count;
            var lastAssigned = workerJobs.Max(j => j.AssignedAt);

            workersList.Add(new WorkerLineDto(
                w.Id,
                w.FullName,
                w.Email ?? string.Empty,
                w.EmployeeNo,
                w.IsActive,
                line.AssemblyLineId,
                line.AssemblyCode,
                line.AssemblyName,
                workload,
                taskCount,
                lastAssigned));
        }

        return new AssemblyLineDetailDto(
            line.AssemblyLineId,
            line.AssemblyCode,
            line.AssemblyName,
            line.Department,
            line.Description,
            line.IsActive,
            line.CreatedAt,
            line.CreatedBy,
            line.UpdatedAt,
            line.UpdatedBy,
            products,
            workersList,
            activeJobs);
    }

    public async Task<AssemblyLine> CreateAssemblyLineAsync(CreateAssemblyLineRequest request, string createdBy, CancellationToken ct = default)
    {
        var code = request.AssemblyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Assembly ID/Code is required.");

        var name = request.AssemblyName.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("Assembly Name is required.");

        var exists = await _db.AssemblyLines.AnyAsync(a => a.AssemblyCode == code, ct);
        if (exists)
            throw new InvalidOperationException($"An assembly line with ID '{code}' already exists.");

        var line = new AssemblyLine
        {
            AssemblyCode = code,
            AssemblyName = name,
            Department = string.IsNullOrWhiteSpace(request.Department) ? "Assembly" : request.Department.Trim(),
            Description = request.Description?.Trim(),
            IsActive = request.IsActive,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy
        };

        _db.AssemblyLines.Add(line);

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = createdBy,
            UserName = createdBy,
            EntityName = nameof(AssemblyLine),
            EntityId = code,
            Action = "Created",
            Changes = JsonSerializer.Serialize(new
            {
                line.AssemblyCode,
                line.AssemblyName,
                line.Department,
                line.IsActive
            })
        });

        await _db.SaveChangesAsync(ct);
        return line;
    }

    public async Task<AssemblyLine> UpdateAssemblyLineAsync(int assemblyLineId, UpdateAssemblyLineRequest request, string updatedBy, CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines.FindAsync([assemblyLineId], ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        var code = request.AssemblyCode.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(code))
            throw new InvalidOperationException("Assembly ID/Code is required.");

        var exists = await _db.AssemblyLines.AnyAsync(a => a.AssemblyCode == code && a.AssemblyLineId != assemblyLineId, ct);
        if (exists)
            throw new InvalidOperationException($"An assembly line with ID '{code}' already exists.");

        var oldCode = line.AssemblyCode;
        var oldName = line.AssemblyName;
        var oldActive = line.IsActive;

        line.AssemblyCode = code;
        line.AssemblyName = request.AssemblyName.Trim();
        line.Department = string.IsNullOrWhiteSpace(request.Department) ? "Assembly" : request.Department.Trim();
        line.Description = request.Description?.Trim();
        line.IsActive = request.IsActive;
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = updatedBy;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(AssemblyLine),
            EntityId = line.AssemblyLineId.ToString(),
            Action = "Updated",
            Changes = JsonSerializer.Serialize(new
            {
                OldCode = oldCode,
                NewCode = code,
                OldName = oldName,
                NewName = line.AssemblyName,
                OldActive = oldActive,
                NewActive = line.IsActive
            })
        });

        await _db.SaveChangesAsync(ct);
        return line;
    }

    public async Task<bool> ToggleAssemblyLineStatusAsync(int assemblyLineId, string updatedBy, CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines.FindAsync([assemblyLineId], ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        line.IsActive = !line.IsActive;
        line.UpdatedAt = DateTime.UtcNow;
        line.UpdatedBy = updatedBy;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(AssemblyLine),
            EntityId = line.AssemblyLineId.ToString(),
            Action = line.IsActive ? "Activated" : "Deactivated",
            Changes = JsonSerializer.Serialize(new
            {
                line.AssemblyCode,
                line.IsActive
            })
        });

        await _db.SaveChangesAsync(ct);
        return line.IsActive;
    }

    public async Task<bool> DeleteAssemblyLineAsync(int assemblyLineId, string updatedBy, CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines
            .Include(a => a.Jobs)
            .Include(a => a.AssignedWorkers)
            .Include(a => a.ProductMappings)
            .FirstOrDefaultAsync(a => a.AssemblyLineId == assemblyLineId, ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        // If there are existing jobs, soft deactivate rather than hard delete to preserve history
        if (line.Jobs.Count > 0)
        {
            line.IsActive = false;
            line.UpdatedAt = DateTime.UtcNow;
            line.UpdatedBy = updatedBy;
            await _db.SaveChangesAsync(ct);
            return false; // Indicates soft-deactivated
        }

        // Unlink workers
        foreach (var worker in line.AssignedWorkers)
        {
            worker.CurrentAssemblyLineId = null;
        }

        _db.ProductAssemblyMappings.RemoveRange(line.ProductMappings);
        _db.AssemblyLines.Remove(line);

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(AssemblyLine),
            EntityId = assemblyLineId.ToString(),
            Action = "Deleted",
            Changes = JsonSerializer.Serialize(new { line.AssemblyCode, line.AssemblyName })
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null, CancellationToken ct = default)
    {
        var normalized = code.Trim().ToUpperInvariant();
        return !await _db.AssemblyLines
            .AnyAsync(a => a.AssemblyCode == normalized && (!excludeId.HasValue || a.AssemblyLineId != excludeId.Value), ct);
    }

    #endregion

    #region Product-to-Assembly Mapping

    public async Task<IReadOnlyList<ProductMappingDto>> GetLineProductMappingsAsync(int assemblyLineId, CancellationToken ct = default)
    {
        return await _db.ProductAssemblyMappings
            .AsNoTracking()
            .Include(m => m.Product)
            .Where(m => m.AssemblyLineId == assemblyLineId)
            .OrderByDescending(m => m.IsDefault)
            .ThenBy(m => m.Product.ProductCode)
            .Select(m => new ProductMappingDto(
                m.ProductAssemblyMappingId,
                m.ProductId,
                m.Product.ProductCode,
                m.Product.ProductName,
                m.Product.Description,
                m.IsDefault,
                m.Product.IsActive,
                m.CreatedAt))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Product>> GetUnmappedProductsAsync(string? search = null, CancellationToken ct = default)
    {
        var allProducts = await _db.Products
            .AsNoTracking()
            .Include(p => p.Components)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(p => p.AssemblyMappings)
                .ThenInclude(m => m.AssemblyLine)
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var unmappedList = new List<Product>();
        foreach (var p in allProducts)
        {
            var totalSteps = p.Components.Sum(c => c.Template?.Steps.Count ?? 0);
            var activeMappings = p.AssemblyMappings.Where(m => m.AssemblyLine != null && m.AssemblyLine.IsActive).ToList();
            if (activeMappings.Count == 0)
            {
                unmappedList.Add(p);
                continue;
            }

            var hasFullProduct = activeMappings.Any(m => m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct);
            if (hasFullProduct) continue;

            var mappedStepIds = new HashSet<int>();
            foreach (var m in activeMappings)
            {
                if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedSteps && !string.IsNullOrWhiteSpace(m.AllowedStepIds))
                {
                    foreach (var s in m.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(s, out var sid)) mappedStepIds.Add(sid);
                    }
                }
                else if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedTemplates && !string.IsNullOrWhiteSpace(m.AllowedTemplateIds))
                {
                    var tids = m.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
                    foreach (var c in p.Components)
                    {
                        if (c.Template != null && tids.Contains(c.TemplateId.ToString()))
                        {
                            foreach (var s in c.Template.Steps) mappedStepIds.Add(s.TemplateStepId);
                        }
                    }
                }
            }

            if (totalSteps == 0 || mappedStepIds.Count < totalSteps)
            {
                unmappedList.Add(p);
            }
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            unmappedList = unmappedList.Where(p => p.ProductCode.Contains(s, StringComparison.OrdinalIgnoreCase) || p.ProductName.Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return unmappedList.OrderBy(p => p.ProductCode).ToList();
    }

    public async Task<IReadOnlyList<ProductWithLineDto>> GetAllProductsWithLineMappingAsync(
        string? search = null,
        int? assemblyLineId = null,
        bool? unmappedOnly = null,
        CancellationToken ct = default)
    {
        var query = _db.Products
            .AsNoTracking()
            .Include(p => p.Components)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(p => p.AssemblyMappings)
                .ThenInclude(m => m.AssemblyLine)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            query = query.Where(p => p.ProductCode.Contains(s) || p.ProductName.Contains(s));
        }

        var products = await query.OrderBy(p => p.ProductCode).ToListAsync(ct);

        var result = new List<ProductWithLineDto>();
        foreach (var p in products)
        {
            var totalSteps = p.Components.Sum(c => c.Template?.Steps.Count ?? 0);
            var activeMappings = p.AssemblyMappings.Where(m => m.AssemblyLine != null && m.AssemblyLine.IsActive).ToList();

            var mappedStepIds = new HashSet<int>();
            var hasFullProduct = activeMappings.Any(m => m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct);
            if (hasFullProduct)
            {
                foreach (var c in p.Components)
                {
                    if (c.Template != null)
                    {
                        foreach (var s in c.Template.Steps) mappedStepIds.Add(s.TemplateStepId);
                    }
                }
            }
            else
            {
                foreach (var m in activeMappings)
                {
                    if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedSteps && !string.IsNullOrWhiteSpace(m.AllowedStepIds))
                    {
                        foreach (var s in m.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            if (int.TryParse(s, out var sid)) mappedStepIds.Add(sid);
                        }
                    }
                    else if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedTemplates && !string.IsNullOrWhiteSpace(m.AllowedTemplateIds))
                    {
                        var tids = m.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
                        foreach (var c in p.Components)
                        {
                            if (c.Template != null && tids.Contains(c.TemplateId.ToString()))
                            {
                                foreach (var s in c.Template.Steps) mappedStepIds.Add(s.TemplateStepId);
                            }
                        }
                    }
                }
            }

            var mappedStepsCount = hasFullProduct ? totalSteps : mappedStepIds.Count;
            var isFullyMapped = totalSteps > 0 ? (mappedStepsCount >= totalSteps) : activeMappings.Count > 0;

            if (unmappedOnly == true && isFullyMapped)
            {
                continue;
            }

            if (assemblyLineId.HasValue)
            {
                activeMappings = activeMappings.Where(m => m.AssemblyLineId == assemblyLineId.Value).ToList();
                if (activeMappings.Count == 0) continue;
            }

            if (activeMappings.Count == 0)
            {
                result.Add(new ProductWithLineDto(
                    p.ProductId,
                    p.ProductCode,
                    p.ProductName,
                    p.IsActive,
                    null,
                    null,
                    null,
                    false,
                    SmartTorque.Core.Enums.AllocationScopeType.FullProduct,
                    null,
                    null,
                    null,
                    totalSteps,
                    mappedStepsCount,
                    isFullyMapped));
            }
            else
            {
                foreach (var m in activeMappings)
                {
                    result.Add(new ProductWithLineDto(
                        p.ProductId,
                        p.ProductCode,
                        p.ProductName,
                        p.IsActive,
                        m.AssemblyLineId,
                        m.AssemblyLine?.AssemblyCode,
                        m.AssemblyLine?.AssemblyName,
                        m.IsDefault,
                        m.ScopeType,
                        m.AllowedTemplateIds,
                        m.AllowedStepIds,
                        m.ScopeSummary,
                        totalSteps,
                        mappedStepsCount,
                        isFullyMapped));
                }
            }
        }

        return result;
    }

    public async Task AssignProductToLineAsync(
        int productId,
        int assemblyLineId,
        bool isDefault,
        string createdBy,
        SmartTorque.Core.Enums.AllocationScopeType scopeType = SmartTorque.Core.Enums.AllocationScopeType.FullProduct,
        string? allowedTemplateIds = null,
        string? allowedStepIds = null,
        string? scopeSummary = null,
        CancellationToken ct = default)
    {
        var product = await _db.Products.FindAsync([productId], ct)
            ?? throw new InvalidOperationException($"Product {productId} not found.");

        if (!product.IsActive)
            throw new InvalidOperationException($"Cannot map inactive product '{product.ProductCode}' to an assembly line.");

        var line = await _db.AssemblyLines.FindAsync([assemblyLineId], ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        if (!line.IsActive)
            throw new InvalidOperationException($"Cannot map product to inactive assembly line '{line.AssemblyCode}'.");

        var existingMappings = await _db.ProductAssemblyMappings
            .Where(m => m.ProductId == productId)
            .ToListAsync(ct);

        var currentLineMapping = existingMappings.FirstOrDefault(m => m.AssemblyLineId == assemblyLineId);

        if (scopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedSteps && !string.IsNullOrWhiteSpace(allowedStepIds))
        {
            var newStepIdSet = allowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
            
            // Clean up these steps from other lines for this product so steps are never duplicated across lines
            foreach (var other in existingMappings.Where(m => m.AssemblyLineId != assemblyLineId && m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedSteps).ToList())
            {
                if (!string.IsNullOrWhiteSpace(other.AllowedStepIds))
                {
                    var otherSteps = other.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Where(id => !newStepIdSet.Contains(id))
                        .ToList();

                    if (otherSteps.Count == 0)
                    {
                        _db.ProductAssemblyMappings.Remove(other);
                    }
                    else
                    {
                        other.AllowedStepIds = string.Join(",", otherSteps);
                        other.ScopeSummary = $"Scoped Steps ({otherSteps.Count} Operations)";
                        other.UpdatedAt = DateTime.UtcNow;
                        other.UpdatedBy = createdBy;
                    }
                }
            }
        }


        // If this is the only mapping or is marked as default, clear default from others
        if (isDefault || existingMappings.Count == 0)
        {
            isDefault = true;
            foreach (var other in existingMappings.Where(m => m.AssemblyLineId != assemblyLineId))
            {
                other.IsDefault = false;
            }
        }

        if (currentLineMapping != null)
        {
            currentLineMapping.IsDefault = isDefault;
            currentLineMapping.ScopeType = scopeType;
            currentLineMapping.AllowedTemplateIds = allowedTemplateIds;
            currentLineMapping.AllowedStepIds = allowedStepIds;
            currentLineMapping.ScopeSummary = scopeSummary;
            currentLineMapping.UpdatedAt = DateTime.UtcNow;
            currentLineMapping.UpdatedBy = createdBy;
        }
        else
        {
            _db.ProductAssemblyMappings.Add(new ProductAssemblyMapping
            {
                ProductId = productId,
                AssemblyLineId = assemblyLineId,
                IsDefault = isDefault,
                ScopeType = scopeType,
                AllowedTemplateIds = allowedTemplateIds,
                AllowedStepIds = allowedStepIds,
                ScopeSummary = scopeSummary,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = createdBy
            });
        }

        // Persist mapping first so synchronization reads the newest database state
        await _db.SaveChangesAsync(ct);

        // Synchronize all open/unassigned jobs for this product to raise requests in their respective assembly lines
        await SyncProductJobsWithMappingsAsync(productId, ct);

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = createdBy,
            UserName = createdBy,
            EntityName = nameof(ProductAssemblyMapping),
            EntityId = $"{productId}-{assemblyLineId}",
            Action = "AssignedToLine",
            Changes = JsonSerializer.Serialize(new
            {
                ProductCode = product.ProductCode,
                AssemblyCode = line.AssemblyCode,
                IsDefault = isDefault
            })
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task RemoveProductFromLineAsync(int productId, int assemblyLineId, string updatedBy, CancellationToken ct = default)
    {
        var mapping = await _db.ProductAssemblyMappings
            .FirstOrDefaultAsync(m => m.ProductId == productId && m.AssemblyLineId == assemblyLineId, ct)
            ?? throw new InvalidOperationException($"Product mapping for product {productId} on line {assemblyLineId} not found.");

        var wasDefault = mapping.IsDefault;
        _db.ProductAssemblyMappings.Remove(mapping);

        // If removed mapping was default, promote another mapping for this product if one exists
        if (wasDefault)
        {
            var remaining = await _db.ProductAssemblyMappings
                .Where(m => m.ProductId == productId && m.AssemblyLineId != assemblyLineId)
                .FirstOrDefaultAsync(ct);

            if (remaining != null)
            {
                remaining.IsDefault = true;
                remaining.UpdatedAt = DateTime.UtcNow;
                remaining.UpdatedBy = updatedBy;
            }
        }

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(ProductAssemblyMapping),
            EntityId = $"{productId}-{assemblyLineId}",
            Action = "RemovedFromLine",
            Changes = JsonSerializer.Serialize(new { ProductId = productId, AssemblyLineId = assemblyLineId })
        });

        await _db.SaveChangesAsync(ct);

        // Re-sync open jobs with remaining line mappings
        await SyncProductJobsWithMappingsAsync(productId, ct);
    }

    public async Task SetDefaultLineForProductAsync(int productId, int assemblyLineId, string updatedBy, CancellationToken ct = default)
    {
        var mappings = await _db.ProductAssemblyMappings
            .Where(m => m.ProductId == productId)
            .ToListAsync(ct);

        var target = mappings.FirstOrDefault(m => m.AssemblyLineId == assemblyLineId)
            ?? throw new InvalidOperationException($"Product {productId} is not mapped to assembly line {assemblyLineId}.");

        foreach (var m in mappings)
        {
            m.IsDefault = (m.AssemblyLineId == assemblyLineId);
            m.UpdatedAt = DateTime.UtcNow;
            m.UpdatedBy = updatedBy;
        }

        // Update pending unassigned jobs to use this default line
        var openJobs = await _db.Jobs
            .Where(j => j.ProductId == productId && j.Status == JobStatus.Pending && j.AssignedWorkerId == null)
            .ToListAsync(ct);

        foreach (var j in openJobs)
        {
            j.AssemblyLineId = assemblyLineId;
            j.UpdatedAt = DateTime.UtcNow;
        }

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(ProductAssemblyMapping),
            EntityId = $"{productId}-{assemblyLineId}",
            Action = "SetDefaultLine",
            Changes = JsonSerializer.Serialize(new { ProductId = productId, DefaultAssemblyLineId = assemblyLineId })
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task SyncAllProductJobsWithMappingsAsync(CancellationToken ct = default)
    {
        var productIds = await _db.ProductAssemblyMappings
            .Select(m => m.ProductId)
            .Distinct()
            .ToListAsync(ct);

        foreach (var pid in productIds)
        {
            await SyncProductJobsWithMappingsAsync(pid, ct);
        }
    }

    public async Task SyncProductJobsWithMappingsAsync(int productId, CancellationToken ct = default)
    {
        var activeMappings = await _db.ProductAssemblyMappings
            .Include(m => m.AssemblyLine)
            .Where(m => m.ProductId == productId && m.AssemblyLine != null && m.AssemblyLine.IsActive)
            .ToListAsync(ct);

        if (activeMappings.Count == 0) return;

        // Find all open jobs for this product that are not completed and not cancelled
        var openJobs = await _db.Jobs
            .Include(j => j.Units)
            .Include(j => j.AssemblyLine)
            .Where(j => j.ProductId == productId && 
                        j.Status != JobStatus.Completed && 
                        j.Status != JobStatus.Cancelled &&
                        j.AssignedWorkerId == null)
            .ToListAsync(ct);

        if (openJobs.Count == 0) return;

        // Group jobs by source work order
        var jobGroups = openJobs.GroupBy(j => j.SourceBmRowId.HasValue 
            ? $"BM-{j.SourceBmRowId}" 
            : (string.IsNullOrWhiteSpace(j.SalesOrder) ? $"JOB-{j.JobId}" : $"SO-{j.SalesOrder}")).ToList();

        foreach (var group in jobGroups)
        {
            var groupJobs = group.ToList();

            // For each active line mapping, ensure a job exists for this line
            foreach (var mapping in activeMappings)
            {
                var lineJob = groupJobs.FirstOrDefault(j => j.AssemblyLineId == mapping.AssemblyLineId);
                if (lineJob != null)
                {
                    // Update scope properties to exactly match the department head's mapping
                    lineJob.ScopeType = mapping.ScopeType;
                    lineJob.AllowedTemplateIds = mapping.AllowedTemplateIds;
                    lineJob.AllowedStepIds = mapping.AllowedStepIds;
                    lineJob.ScopeSummary = mapping.ScopeSummary ?? (mapping.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct ? "Full Product" : null);
                    lineJob.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    // If there's an unassigned job without a line in this group, assign it to this line
                    var unassignedJob = groupJobs.FirstOrDefault(j => j.AssemblyLineId == null);
                    if (unassignedJob != null)
                    {
                        unassignedJob.AssemblyLineId = mapping.AssemblyLineId;
                        unassignedJob.ScopeType = mapping.ScopeType;
                        unassignedJob.AllowedTemplateIds = mapping.AllowedTemplateIds;
                        unassignedJob.AllowedStepIds = mapping.AllowedStepIds;
                        unassignedJob.ScopeSummary = mapping.ScopeSummary ?? (mapping.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct ? "Full Product" : null);
                        unassignedJob.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Raise a job request in this assembly line
                        var templateJob = groupJobs.First();
                        var lineCode = mapping.AssemblyLine?.AssemblyCode ?? $"L{mapping.AssemblyLineId}";
                        var newJobNo = $"{templateJob.JobNo}-{lineCode}";

                        var attempt = 1;
                        while (await _db.Jobs.AnyAsync(j => j.JobNo == newJobNo, ct))
                        {
                            newJobNo = $"{templateJob.JobNo}-{lineCode}-P{attempt++}";
                        }

                        var newJob = new Job
                        {
                            JobNo = newJobNo,
                            ProductId = productId,
                            AssemblyLineId = mapping.AssemblyLineId,
                            EstimatedWorkload = templateJob.EstimatedWorkload,
                            ProductSerial = templateJob.ProductSerial,
                            SalesOrder = templateJob.SalesOrder,
                            PONo = templateJob.PONo,
                            Quantity = templateJob.Quantity,
                            Priority = templateJob.Priority,
                            DueDate = templateJob.DueDate,
                            Status = JobStatus.Pending,
                            AssignmentMode = templateJob.AssignmentMode,
                            ExecutionMode = templateJob.ExecutionMode,
                            SourceBmRowId = templateJob.SourceBmRowId,
                            ScopeType = mapping.ScopeType,
                            AllowedTemplateIds = mapping.AllowedTemplateIds,
                            AllowedStepIds = mapping.AllowedStepIds,
                            ScopeSummary = mapping.ScopeSummary ?? (mapping.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct ? "Full Product" : null),
                            CreatedAt = DateTime.UtcNow
                        };

                        for (var u = 1; u <= templateJob.Quantity; u++)
                        {
                            newJob.Units.Add(new JobUnit
                            {
                                UnitIndex = u,
                                TokenNo = TokenGenerator.GenerateProductUnitToken(),
                                Status = UnitAssemblyStatus.Pending,
                                CreatedAt = DateTime.UtcNow
                            });
                        }

                        _db.Jobs.Add(newJob);
                        groupJobs.Add(newJob);
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);
    }


    #endregion

    #region Worker Assembly Assignment

    public async Task<IReadOnlyList<WorkerLineDto>> GetWorkersForLineAsync(int assemblyLineId, CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssemblyLineId == assemblyLineId, ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        var workers = await _db.Users
            .AsNoTracking()
            .Where(u => u.CurrentAssemblyLineId == assemblyLineId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var workerIds = workers.Select(w => w.Id).ToList();

        var activeJobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedWorkerId != null && workerIds.Contains(j.AssignedWorkerId) &&
                        (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress))
            .ToListAsync(ct);

        return workers.Select(w =>
        {
            var userJobs = activeJobs.Where(j => j.AssignedWorkerId == w.Id).ToList();
            var workload = userJobs.Sum(j => j.EstimatedWorkload);
            var taskCount = userJobs.Count;
            var lastAssigned = userJobs.Max(j => j.AssignedAt);

            return new WorkerLineDto(
                w.Id,
                w.FullName,
                w.Email ?? string.Empty,
                w.EmployeeNo,
                w.IsActive,
                line.AssemblyLineId,
                line.AssemblyCode,
                line.AssemblyName,
                workload,
                taskCount,
                lastAssigned);
        }).ToList();
    }

    public async Task<IReadOnlyList<WorkerLineDto>> GetAllActiveWorkersWithLineAsync(CancellationToken ct = default)
    {
        var workerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Worker", ct);
        if (workerRole == null)
            return [];

        var workerUserIds = await _db.UserRoles
            .Where(ur => ur.RoleId == workerRole.Id)
            .Select(ur => ur.UserId)
            .ToListAsync(ct);

        var workers = await _db.Users
            .AsNoTracking()
            .Include(u => u.CurrentAssemblyLine)
            .Where(u => workerUserIds.Contains(u.Id) && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync(ct);

        var activeJobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedWorkerId != null && workerUserIds.Contains(j.AssignedWorkerId) &&
                        (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress))
            .ToListAsync(ct);

        return workers.Select(w =>
        {
            var userJobs = activeJobs.Where(j => j.AssignedWorkerId == w.Id).ToList();
            var workload = userJobs.Sum(j => j.EstimatedWorkload);
            var taskCount = userJobs.Count;
            var lastAssigned = userJobs.Max(j => j.AssignedAt);

            return new WorkerLineDto(
                w.Id,
                w.FullName,
                w.Email ?? string.Empty,
                w.EmployeeNo,
                w.IsActive,
                w.CurrentAssemblyLineId,
                w.CurrentAssemblyLine?.AssemblyCode,
                w.CurrentAssemblyLine?.AssemblyName,
                workload,
                taskCount,
                lastAssigned);
        }).ToList();
    }

    public async Task SetWorkerAssemblyLineAsync(string workerId, int? assemblyLineId, string updatedBy, CancellationToken ct = default)
    {
        var worker = await _db.Users.FindAsync([workerId], ct)
            ?? throw new InvalidOperationException($"Worker '{workerId}' not found.");

        AssemblyLine? targetLine = null;
        if (assemblyLineId.HasValue)
        {
            targetLine = await _db.AssemblyLines.FindAsync([assemblyLineId.Value], ct)
                ?? throw new InvalidOperationException($"Assembly line {assemblyLineId.Value} not found.");

            if (!targetLine.IsActive)
                throw new InvalidOperationException($"Cannot assign worker to inactive assembly line '{targetLine.AssemblyCode}'.");
        }

        var prevLineId = worker.CurrentAssemblyLineId;
        worker.CurrentAssemblyLineId = assemblyLineId;
        worker.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = updatedBy,
            UserName = updatedBy,
            EntityName = nameof(ApplicationUser),
            EntityId = worker.Id,
            Action = "ChangedAssemblyLine",
            Changes = JsonSerializer.Serialize(new
            {
                WorkerName = worker.FullName,
                PreviousLineId = prevLineId,
                NewLineId = assemblyLineId,
                AssemblyCode = targetLine?.AssemblyCode
            })
        });

        await _db.SaveChangesAsync(ct);
    }

    public async Task<AssemblyLine?> GetWorkerCurrentLineAsync(string workerId, CancellationToken ct = default)
    {
        var worker = await _db.Users
            .AsNoTracking()
            .Include(u => u.CurrentAssemblyLine)
            .FirstOrDefaultAsync(u => u.Id == workerId, ct);

        return worker?.CurrentAssemblyLine;
    }

    #endregion

    #region Dashboard

    public async Task<AssemblyDashboardDto> GetDashboardStatsAsync(CancellationToken ct = default)
    {
        var lines = await GetAssemblyLinesAsync(ct: ct);

        var totalLines = lines.Count;
        var activeLines = lines.Count(l => l.IsActive);
        var inactiveLines = totalLines - activeLines;

        var allProducts = await _db.Products
            .AsNoTracking()
            .Include(p => p.Components)
                .ThenInclude(c => c.Template)
                    .ThenInclude(t => t.Steps)
            .Include(p => p.AssemblyMappings)
                .ThenInclude(m => m.AssemblyLine)
            .Where(p => p.IsActive)
            .ToListAsync(ct);

        var totalMappedProducts = 0;
        var unmappedCount = 0;

        foreach (var p in allProducts)
        {
            var totalSteps = p.Components.Sum(c => c.Template?.Steps.Count ?? 0);
            var activeMappings = p.AssemblyMappings.Where(m => m.AssemblyLine != null && m.AssemblyLine.IsActive).ToList();

            if (activeMappings.Count == 0)
            {
                unmappedCount++;
                continue;
            }

            var hasFullProduct = activeMappings.Any(m => m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.FullProduct);
            if (hasFullProduct)
            {
                totalMappedProducts++;
                continue;
            }

            var mappedStepIds = new HashSet<int>();
            foreach (var m in activeMappings)
            {
                if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedSteps && !string.IsNullOrWhiteSpace(m.AllowedStepIds))
                {
                    foreach (var s in m.AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(s, out var sid)) mappedStepIds.Add(sid);
                    }
                }
                else if (m.ScopeType == SmartTorque.Core.Enums.AllocationScopeType.SelectedTemplates && !string.IsNullOrWhiteSpace(m.AllowedTemplateIds))
                {
                    var tids = m.AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
                    foreach (var c in p.Components)
                    {
                        if (c.Template != null && tids.Contains(c.TemplateId.ToString()))
                        {
                            foreach (var s in c.Template.Steps) mappedStepIds.Add(s.TemplateStepId);
                        }
                    }
                }
            }

            if (totalSteps > 0 && mappedStepIds.Count >= totalSteps)
            {
                totalMappedProducts++;
            }
            else
            {
                unmappedCount++;
            }
        }

        var workerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Worker", ct);
        var workerUserIds = workerRole != null
            ? await _db.UserRoles.Where(ur => ur.RoleId == workerRole.Id).Select(ur => ur.UserId).ToListAsync(ct)
            : new List<string>();

        var totalActiveWorkers = await _db.Users
            .CountAsync(u => workerUserIds.Contains(u.Id) && u.IsActive, ct);

        var workersAssignedToLines = await _db.Users
            .CountAsync(u => workerUserIds.Contains(u.Id) && u.IsActive && u.CurrentAssemblyLineId != null, ct);

        var totalPendingTasks = lines.Sum(l => l.PendingTaskCount);
        var totalInProgressTasks = lines.Sum(l => l.InProgressTaskCount);

        return new AssemblyDashboardDto(
            totalLines,
            activeLines,
            inactiveLines,
            totalMappedProducts,
            unmappedCount,
            totalActiveWorkers,
            workersAssignedToLines,
            totalPendingTasks,
            totalInProgressTasks,
            lines);
    }

    #endregion

    #region Workload Calculation & Task Allocation

    public async Task<decimal> GetWorkerActiveWorkloadAsync(string workerId, CancellationToken ct = default)
    {
        return await _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedWorkerId == workerId &&
                        (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress))
            .SumAsync(j => j.EstimatedWorkload, ct);
    }

    public async Task<AutoAssignResult> AutoAssignJobsAsync(string changedBy, int? specificAssemblyLineId = null, CancellationToken ct = default)
    {
        // 1. Fetch unassigned pending jobs
        var unassignedJobsQuery = _db.Jobs
            .Include(j => j.Product)
            .Where(j => j.Status == JobStatus.Pending && j.AssignedWorkerId == null);

        if (specificAssemblyLineId.HasValue)
        {
            unassignedJobsQuery = unassignedJobsQuery.Where(j => j.AssemblyLineId == specificAssemblyLineId.Value);
        }

        var unassignedJobs = await _priorities
            .OrderForWorkboard(unassignedJobsQuery)
            .ToListAsync(ct);

        if (unassignedJobs.Count == 0)
        {
            return new AutoAssignResult(0, 0, []);
        }

        // 2. Resolve missing AssemblyLineId for jobs using product default mappings
        var productIds = unassignedJobs.Where(j => j.AssemblyLineId == null).Select(j => j.ProductId).Distinct().ToList();
        if (productIds.Count > 0)
        {
            var defaultMappings = await _db.ProductAssemblyMappings
                .Where(m => productIds.Contains(m.ProductId) && m.IsDefault)
                .ToDictionaryAsync(m => m.ProductId, m => m.AssemblyLineId, ct);

            foreach (var j in unassignedJobs.Where(j => j.AssemblyLineId == null))
            {
                if (defaultMappings.TryGetValue(j.ProductId, out var lineId))
                {
                    j.AssemblyLineId = lineId;
                }
            }
        }

        // 3. Group jobs by AssemblyLineId
        var groupedByLine = unassignedJobs.GroupBy(j => j.AssemblyLineId).ToList();

        var totalAssigned = 0;
        var totalUnassigned = 0;
        var summaries = new List<LineAssignSummary>();

        // Preload active lines
        var lineIds = groupedByLine.Where(g => g.Key.HasValue).Select(g => g.Key!.Value).Distinct().ToList();
        var assemblyLines = await _db.AssemblyLines
            .Where(a => lineIds.Contains(a.AssemblyLineId))
            .ToDictionaryAsync(a => a.AssemblyLineId, ct);

        // Preload all active workers with worker role
        var workerRole = await _db.Roles.FirstOrDefaultAsync(r => r.Name == "Worker", ct);
        var workerUserIds = workerRole != null
            ? await _db.UserRoles.Where(ur => ur.RoleId == workerRole.Id).Select(ur => ur.UserId).ToListAsync(ct)
            : new List<string>();

        var allActiveWorkers = await _db.Users
            .Where(u => workerUserIds.Contains(u.Id) && u.IsActive && u.CurrentAssemblyLineId != null)
            .ToListAsync(ct);

        // Preload current workloads and active task counts for all active workers
        var activeJobs = await _db.Jobs
            .AsNoTracking()
            .Where(j => j.AssignedWorkerId != null &&
                        (j.Status == JobStatus.Assigned || j.Status == JobStatus.InProgress))
            .ToListAsync(ct);

        var workerWorkloads = allActiveWorkers.ToDictionary(
            w => w.Id,
            w => activeJobs.Where(j => j.AssignedWorkerId == w.Id).Sum(j => j.EstimatedWorkload));

        var workerTaskCounts = allActiveWorkers.ToDictionary(
            w => w.Id,
            w => activeJobs.Count(j => j.AssignedWorkerId == w.Id));

        var workerLastAssigned = allActiveWorkers.ToDictionary(
            w => w.Id,
            w => activeJobs.Where(j => j.AssignedWorkerId == w.Id && j.AssignedAt.HasValue).Select(j => j.AssignedAt!.Value).DefaultIfEmpty(DateTime.MinValue).Max());

        foreach (var group in groupedByLine)
        {
            var lineId = group.Key;
            var jobsInGroup = group.ToList();

            if (!lineId.HasValue)
            {
                // Unmapped jobs: distribute across active workers not bound to a specific line
                var poolWorkers = await _db.Users
                    .Where(u => workerUserIds.Contains(u.Id) && u.IsActive && u.CurrentAssemblyLineId == null)
                    .ToListAsync(ct);

                if (poolWorkers.Count == 0)
                {
                    totalUnassigned += jobsInGroup.Count;
                    summaries.Add(new LineAssignSummary(
                        0,
                        "UNMAPPED",
                        "No Assembly Line Mapped",
                        0,
                        jobsInGroup.Count,
                        "No workers in general pool and no assembly line mapped."));
                    continue;
                }

                var poolWorkloads = poolWorkers.ToDictionary(
                    w => w.Id,
                    w => activeJobs.Where(j => j.AssignedWorkerId == w.Id).Sum(j => j.EstimatedWorkload));
                var poolTaskCounts = poolWorkers.ToDictionary(
                    w => w.Id,
                    w => activeJobs.Count(j => j.AssignedWorkerId == w.Id));
                var poolLastAssigned = poolWorkers.ToDictionary(
                    w => w.Id,
                    w => activeJobs.Where(j => j.AssignedWorkerId == w.Id && j.AssignedAt.HasValue).Select(j => j.AssignedAt!.Value).DefaultIfEmpty(DateTime.MinValue).Max());

                var poolAssigned = 0;
                foreach (var job in jobsInGroup)
                {
                    var chosenWorker = poolWorkers
                        .OrderBy(w => poolWorkloads[w.Id])
                        .ThenBy(w => poolTaskCounts[w.Id])
                        .ThenBy(w => poolLastAssigned[w.Id])
                        .ThenBy(w => w.Id)
                        .First();

                    job.AssignedWorkerId = chosenWorker.Id;
                    job.Status = JobStatus.Assigned;
                    job.AssignmentMode = AssignmentMode.Auto;
                    job.AssignedAt = DateTime.UtcNow;
                    job.UpdatedAt = DateTime.UtcNow;

                    poolWorkloads[chosenWorker.Id] += job.EstimatedWorkload;
                    poolTaskCounts[chosenWorker.Id] += 1;
                    poolLastAssigned[chosenWorker.Id] = DateTime.UtcNow;

                    poolAssigned++;
                    totalAssigned++;
                }

                summaries.Add(new LineAssignSummary(0, "UNMAPPED", "General Pool", poolAssigned, 0, null));
                continue;
            }

            AssemblyLine? line = null;
            var hasLine = assemblyLines.TryGetValue(lineId.Value, out line) && line.IsActive;

            if (!hasLine || line == null)
            {
                // Inactive line -> Cannot auto-assign; keep Pending
                totalUnassigned += jobsInGroup.Count;
                summaries.Add(new LineAssignSummary(
                    lineId.Value,
                    line?.AssemblyCode ?? "INACTIVE",
                    line?.AssemblyName ?? "Inactive Assembly Line",
                    0,
                    jobsInGroup.Count,
                    "Assembly line is inactive."));
                continue;
            }

            // Find workers on this exact assembly line (STRICT BOUNDARY)
            var eligibleWorkers = allActiveWorkers.Where(w => w.CurrentAssemblyLineId == line.AssemblyLineId).ToList();

            if (eligibleWorkers.Count == 0)
            {
                // Fallback: No workers on this line. Tasks remain Pending
                totalUnassigned += jobsInGroup.Count;

                // Raise Admin alert notification
                _db.Notifications.Add(_notifications.CreateAssemblyAlertNotice(
                    line.AssemblyCode,
                    line.AssemblyName,
                    jobsInGroup.Count));

                summaries.Add(new LineAssignSummary(
                    line.AssemblyLineId,
                    line.AssemblyCode,
                    line.AssemblyName,
                    0,
                    jobsInGroup.Count,
                    "No active workers currently assigned to this assembly line. Alert generated for Admin."));
                continue;
            }

            var lineAssigned = 0;
            foreach (var job in jobsInGroup)
            {
                // Pick least-loaded worker on this line with deterministic tie-breaking:
                // 1. Lowest active workload (Sum of EstimatedWorkload)
                // 2. Fewer active tasks
                // 3. Oldest last assigned time
                // 4. Stable worker ID
                var chosenWorker = eligibleWorkers
                    .OrderBy(w => workerWorkloads[w.Id])
                    .ThenBy(w => workerTaskCounts[w.Id])
                    .ThenBy(w => workerLastAssigned[w.Id])
                    .ThenBy(w => w.Id)
                    .First();

                job.AssignedWorkerId = chosenWorker.Id;
                job.Status = JobStatus.Assigned;
                job.AssignmentMode = AssignmentMode.Auto;
                job.AssignedAt = DateTime.UtcNow;
                job.UpdatedAt = DateTime.UtcNow;

                // Update local workload tracking
                workerWorkloads[chosenWorker.Id] += job.EstimatedWorkload;
                workerTaskCounts[chosenWorker.Id] += 1;
                workerLastAssigned[chosenWorker.Id] = DateTime.UtcNow;

                lineAssigned++;
                totalAssigned++;
            }

            summaries.Add(new LineAssignSummary(
                line.AssemblyLineId,
                line.AssemblyCode,
                line.AssemblyName,
                lineAssigned,
                0,
                null));
        }

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = "BatchAutoAssign",
            Action = "WorkloadAutoAssign",
            Changes = JsonSerializer.Serialize(new
            {
                TotalAssigned = totalAssigned,
                TotalUnassigned = totalUnassigned,
                Summaries = summaries
            })
        });

        await _db.SaveChangesAsync(ct);

        return new AutoAssignResult(totalAssigned, totalUnassigned, summaries);
    }

    public async Task<Job> ManualReassignJobAsync(
        int jobId,
        string? workerId,
        decimal? estimatedWorkload,
        int? assemblyLineId,
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
        var oldLineId = job.AssemblyLineId;
        var oldWorkload = job.EstimatedWorkload;
        var oldStatus = job.Status;

        if (assemblyLineId.HasValue)
        {
            var targetLine = await _db.AssemblyLines.FindAsync([assemblyLineId.Value], ct)
                ?? throw new InvalidOperationException($"Assembly line {assemblyLineId.Value} not found.");

            if (!targetLine.IsActive)
                throw new InvalidOperationException($"Cannot assign task to inactive assembly line '{targetLine.AssemblyCode}'.");

            job.AssemblyLineId = targetLine.AssemblyLineId;
        }

        if (estimatedWorkload.HasValue && estimatedWorkload.Value > 0)
        {
            job.EstimatedWorkload = estimatedWorkload.Value;
        }

        if (!string.IsNullOrWhiteSpace(workerId))
        {
            var worker = await _db.Users.FindAsync([workerId], ct)
                ?? throw new InvalidOperationException($"Worker '{workerId}' not found.");

            if (!worker.IsActive)
                throw new InvalidOperationException($"Cannot assign job to inactive worker '{worker.FullName}'.");

            // Strict boundary: check worker's current assembly line
            if (job.AssemblyLineId.HasValue && worker.CurrentAssemblyLineId != job.AssemblyLineId.Value)
            {
                var line = await _db.AssemblyLines.FindAsync([job.AssemblyLineId.Value], ct);
                var workerLine = worker.CurrentAssemblyLineId.HasValue
                    ? await _db.AssemblyLines.FindAsync([worker.CurrentAssemblyLineId.Value], ct)
                    : null;

                throw new InvalidOperationException(
                    $"Worker '{worker.FullName}' is assigned to assembly line '{workerLine?.AssemblyCode ?? "None"}'. " +
                    $"Tasks for assembly line '{line?.AssemblyCode}' can only be assigned to workers operating on that line.");
            }

            job.AssignedWorkerId = worker.Id;
            job.AssignedAt = DateTime.UtcNow;
            if (job.Status == JobStatus.Pending)
            {
                job.Status = JobStatus.Assigned;
            }
            job.AssignmentMode = AssignmentMode.Manual;
        }
        else if (workerId != null && string.IsNullOrWhiteSpace(workerId))
        {
            // Explicit unassignment
            job.AssignedWorkerId = null;
            job.AssignedAt = null;
            if (job.Status == JobStatus.Assigned)
            {
                job.Status = JobStatus.Pending;
            }
            job.AssignmentMode = AssignmentMode.Manual;
        }

        // If line was changed and existing assigned worker is not on new line, unassign
        if (job.AssemblyLineId.HasValue && job.AssignedWorkerId != null)
        {
            var currentAssignedWorker = await _db.Users.FindAsync([job.AssignedWorkerId], ct);
            if (currentAssignedWorker != null && currentAssignedWorker.CurrentAssemblyLineId != job.AssemblyLineId.Value)
            {
                job.AssignedWorkerId = null;
                job.AssignedAt = null;
                job.Status = JobStatus.Pending;
            }
        }

        job.UpdatedAt = DateTime.UtcNow;

        _db.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTime.UtcNow,
            UserId = changedBy,
            UserName = changedBy,
            EntityName = nameof(Job),
            EntityId = job.JobId.ToString(),
            Action = "ManualReassign",
            Changes = JsonSerializer.Serialize(new
            {
                job.JobNo,
                PreviousWorkerId = oldWorkerId,
                NewWorkerId = job.AssignedWorkerId,
                PreviousLineId = oldLineId,
                NewLineId = job.AssemblyLineId,
                PreviousWorkload = oldWorkload,
                NewWorkload = job.EstimatedWorkload,
                PreviousStatus = oldStatus,
                NewStatus = job.Status
            })
        });

        await _db.SaveChangesAsync(ct);
        return job;
    }

    public async Task<IReadOnlyList<AssemblyTaskDto>> GetTasksForLineAsync(
        int assemblyLineId,
        JobStatus? status = null,
        CancellationToken ct = default)
    {
        var line = await _db.AssemblyLines
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssemblyLineId == assemblyLineId, ct)
            ?? throw new InvalidOperationException($"Assembly line {assemblyLineId} not found.");

        IQueryable<Job> query = _db.Jobs
            .AsNoTracking()
            .Include(j => j.Product)
            .Include(j => j.AssignedWorker)
            .Where(j => j.AssemblyLineId == assemblyLineId);

        if (status.HasValue)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        var jobs = await _priorities.OrderForWorkboard(query).ToListAsync(ct);

        return jobs.Select(j => new AssemblyTaskDto(
            j.JobId,
            j.JobNo,
            j.ProductId,
            j.Product?.ProductCode ?? string.Empty,
            j.Product?.ProductName ?? string.Empty,
            j.AssemblyLineId,
            line.AssemblyCode,
            j.AssignedWorkerId,
            j.AssignedWorker?.FullName ?? j.AssignedWorker?.UserName,
            j.Status,
            j.Priority,
            j.EstimatedWorkload,
            j.Quantity,
            j.QuantityCompleted,
            j.DueDate,
            j.AssignedAt,
            j.CreatedAt)).ToList();
    }

    #endregion
}