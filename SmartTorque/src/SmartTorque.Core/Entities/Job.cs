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

    public JobStatus Status { get; set; } = JobStatus.Pending;

    /// <summary>PLC hook, unused; always Manual for now.</summary>
    public ExecutionMode ExecutionMode { get; set; } = ExecutionMode.Manual;

    /// <summary>BM row this job came from; used by the re-upload refresh rule.</summary>
    public int? SourceBmRowId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    /// <summary>Optimistic concurrency token for taking a job.</summary>
    public byte[] RowVersion { get; set; } = [];

    // Navigation
    public Product Product { get; set; } = null!;

    public ApplicationUser? AssignedWorker { get; set; }

    public BmRow? SourceBmRow { get; set; }

    /// <summary>Individual product units with 40-character unique tracking tokens.</summary>
    public List<JobUnit> Units { get; set; } = [];
}