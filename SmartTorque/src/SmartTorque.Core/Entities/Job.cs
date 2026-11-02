using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Entities;

/// <summary>
/// A unit of work for one product, created from a BM row (PRD §5.4).
/// <see cref="RowVersion"/> is the optimistic lock used when a worker takes the job.
/// </summary>
public class Job
{
    public int JobId { get; set; }

    /// <summary>`JOB-yyyyMMdd-####`, unique.</summary>
    public string JobNo { get; set; } = string.Empty;

    public int ProductId { get; set; }

    /// <summary>Optional per-unit serial; serial generation is P4.6/feature 6.</summary>
    public string? ProductSerial { get; set; }

    /// <summary>Sales Order number from the BM row or manual entry.</summary>
    public string? SalesOrder { get; set; }

    /// <summary>Purchase Order number from the BM row or manual entry.</summary>
    public string? PONo { get; set; }

    /// <summary>Number of units required, from the BM row.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Units whose sessions have all passed.</summary>
    public int QuantityCompleted { get; set; }

    /// <summary>1 Black … 5 White. Lower sorts first.</summary>
    public PriorityLevel Priority { get; set; } = PriorityLevel.White;

    public DateOnly? DueDate { get; set; }

    public AssignmentMode AssignmentMode { get; set; } = AssignmentMode.Manual;

    public string? AssignedWorkerId { get; set; }

    /// <summary>Timestamp when the job/task was assigned to a worker.</summary>
    public DateTime? AssignedAt { get; set; }

    /// <summary>Designated assembly line responsible for executing this job/task.</summary>
    public int? AssemblyLineId { get; set; }

    /// <summary>Estimated workload / effort units (consumed worker capacity until completed or cancelled).</summary>
    public decimal EstimatedWorkload { get; set; } = 1.0m;

    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>PLC hook, unused; always Manual for now.</summary>
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Manual;

    /// <summary>BM row this job came from; used by the re-upload refresh rule.</summary>
    public int? SourceBmRowId { get; set; }

    /// <summary>Parent job/order ID if this job is an allocated sub-package.</summary>
    public int? ParentJobId { get; set; }

    public Job? ParentJob { get; set; }

    public List<Job> ChildPackages { get; set; } = [];

    /// <summary>Scope of allocation: FullProduct = 1, SelectedTemplates = 2, SelectedSteps = 3.</summary>
    public AllocationScopeType ScopeType { get; set; } = AllocationScopeType.FullProduct;

    /// <summary>Comma-separated TemplateIds assigned to this job package, or null for all templates.</summary>
    public string? AllowedTemplateIds { get; set; }

    /// <summary>Comma-separated TemplateStepIds assigned to this job package, or null for all steps.</summary>
    public string? AllowedStepIds { get; set; }

    /// <summary>Human-readable scope description, e.g. "Templates: Back Enclosure Panel" or "Steps 1–3".</summary>
    public string? ScopeSummary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token for taking a job.</summary>
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Product Product { get; set; } = null!;

    public AssemblyLine? AssemblyLine { get; set; }

    public ApplicationUser? AssignedWorker { get; set; }

    /// <summary>Supervisor overseeing this job's allocation to line workers.</summary>
    public string? AssignedSupervisorId { get; set; }

    public ApplicationUser? AssignedSupervisor { get; set; }

    public BmRow? SourceBmRow { get; set; }

    /// <summary>Individual product units with 40-character unique tracking tokens.</summary>
    public List<JobUnit> Units { get; set; } = [];

    public bool IsTemplateAllowed(int templateId)
    {
        if (ScopeType == AllocationScopeType.FullProduct || string.IsNullOrWhiteSpace(AllowedTemplateIds))
            return true;

        var ids = AllowedTemplateIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Contains(templateId.ToString());
    }

    public bool IsStepAllowed(int stepId)
    {
        if (ScopeType != AllocationScopeType.SelectedSteps || string.IsNullOrWhiteSpace(AllowedStepIds))
            return true;

        var ids = AllowedStepIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return ids.Contains(stepId.ToString());
    }
}
