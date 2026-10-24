using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Services;

#region DTOs and Request Models

public record AssemblyLineDto(
    int AssemblyLineId,
    string AssemblyCode,
    string AssemblyName,
    string Department,
    string? Description,
    bool IsActive,
    int AssignedProductCount,
    int AssignedWorkerCount,
    int PendingTaskCount,
    int InProgressTaskCount,
    decimal TotalActiveWorkload,
    DateTime CreatedAt);

public record AssemblyLineDetailDto(
    int AssemblyLineId,
    string AssemblyCode,
    string AssemblyName,
    string Department,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    string? CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    IReadOnlyList<ProductMappingDto> Products,
    IReadOnlyList<WorkerLineDto> Workers,
    IReadOnlyList<AssemblyTaskDto> ActiveTasks);

public record ProductMappingDto(
    int ProductAssemblyMappingId,
    int ProductId,
    string ProductCode,
    string ProductName,
    string? Description,
    bool IsDefault,
    bool IsProductActive,
    DateTime CreatedAt);

public record ProductWithLineDto(
    int ProductId,
    string ProductCode,
    string ProductName,
    bool IsActive,
    int? AssemblyLineId,
    string? AssemblyCode,
    string? AssemblyName,
    bool IsDefault);

public record WorkerLineDto(
    string WorkerId,
    string FullName,
    string Email,
    string? EmployeeNo,
    bool IsActive,
    int? CurrentAssemblyLineId,
    string? CurrentAssemblyCode,
    string? CurrentAssemblyName,
    decimal ActiveWorkload,
    int ActiveTaskCount,
    DateTime? LastAssignedAt);

public record AssemblyTaskDto(
    int JobId,
    string JobNo,
    int ProductId,
    string ProductCode,
    string ProductName,
    int? AssemblyLineId,
    string? AssemblyCode,
    string? AssignedWorkerId,
    string? AssignedWorkerName,
    JobStatus Status,
    PriorityLevel Priority,
    decimal EstimatedWorkload,
    int Quantity,
    int QuantityCompleted,
    DateOnly? DueDate,
    DateTime? AssignedAt,
    DateTime CreatedAt);

public record AssemblyDashboardDto(
    int TotalAssemblyLines,
    int ActiveAssemblyLines,
    int InactiveAssemblyLines,
    int TotalMappedProducts,
    int UnmappedProductsCount,
    int TotalActiveWorkers,
    int WorkersAssignedToLines,
    int TotalPendingTasks,
    int TotalInProgressTasks,
    IReadOnlyList<AssemblyLineDto> Lines);

public record CreateAssemblyLineRequest(
    string AssemblyCode,
    string AssemblyName,
    string Department,
    string? Description = null,
    bool IsActive = true);

public record UpdateAssemblyLineRequest(
    string AssemblyCode,
    string AssemblyName,
    string Department,
    string? Description = null,
    bool IsActive = true);

public record AutoAssignResult(
    int TotalAssigned,
    int TotalUnassigned,
    IReadOnlyList<LineAssignSummary> LineSummaries);

public record LineAssignSummary(
    int AssemblyLineId,
    string AssemblyCode,
    string AssemblyName,
    int AssignedCount,
    int UnassignedCount,
    string? Reason);

#endregion

public interface IAssemblyService
{
    // Assembly Lines
    Task<IReadOnlyList<AssemblyLineDto>> GetAssemblyLinesAsync(
        string? search = null,
        string? department = null,
        bool? isActive = null,
        CancellationToken ct = default);

    Task<AssemblyLineDetailDto?> GetAssemblyLineDetailAsync(int assemblyLineId, CancellationToken ct = default);

    Task<AssemblyLine> CreateAssemblyLineAsync(CreateAssemblyLineRequest request, string createdBy, CancellationToken ct = default);

    Task<AssemblyLine> UpdateAssemblyLineAsync(int assemblyLineId, UpdateAssemblyLineRequest request, string updatedBy, CancellationToken ct = default);

    Task<bool> ToggleAssemblyLineStatusAsync(int assemblyLineId, string updatedBy, CancellationToken ct = default);

    Task<bool> DeleteAssemblyLineAsync(int assemblyLineId, string updatedBy, CancellationToken ct = default);

    Task<bool> IsCodeUniqueAsync(string code, int? excludeId = null, CancellationToken ct = default);

    // Product-to-Assembly Mapping
    Task<IReadOnlyList<ProductMappingDto>> GetLineProductMappingsAsync(int assemblyLineId, CancellationToken ct = default);

    Task<IReadOnlyList<Product>> GetUnmappedProductsAsync(string? search = null, CancellationToken ct = default);

    Task<IReadOnlyList<ProductWithLineDto>> GetAllProductsWithLineMappingAsync(
        string? search = null,
        int? assemblyLineId = null,
        bool? unmappedOnly = null,
        CancellationToken ct = default);

    Task AssignProductToLineAsync(int productId, int assemblyLineId, bool isDefault, string createdBy, CancellationToken ct = default);

    Task RemoveProductFromLineAsync(int productId, int assemblyLineId, string updatedBy, CancellationToken ct = default);

    Task SetDefaultLineForProductAsync(int productId, int assemblyLineId, string updatedBy, CancellationToken ct = default);

    // Worker Assembly Line Selection & Visibility
    Task<IReadOnlyList<WorkerLineDto>> GetWorkersForLineAsync(int assemblyLineId, CancellationToken ct = default);

    Task<IReadOnlyList<WorkerLineDto>> GetAllActiveWorkersWithLineAsync(CancellationToken ct = default);

    Task SetWorkerAssemblyLineAsync(string workerId, int? assemblyLineId, string updatedBy, CancellationToken ct = default);

    Task<AssemblyLine?> GetWorkerCurrentLineAsync(string workerId, CancellationToken ct = default);

    // Dashboard
    Task<AssemblyDashboardDto> GetDashboardStatsAsync(CancellationToken ct = default);

    // Workload-Based Automatic Task Allocation & Reassignment
    Task<decimal> GetWorkerActiveWorkloadAsync(string workerId, CancellationToken ct = default);

    Task<AutoAssignResult> AutoAssignJobsAsync(string changedBy, int? specificAssemblyLineId = null, CancellationToken ct = default);

    Task<Job> ManualReassignJobAsync(
        int jobId,
        string? workerId,
        decimal? estimatedWorkload,
        int? assemblyLineId,
        string changedBy,
        CancellationToken ct = default);

    Task<IReadOnlyList<AssemblyTaskDto>> GetTasksForLineAsync(
        int assemblyLineId,
        JobStatus? status = null,
        CancellationToken ct = default);
}