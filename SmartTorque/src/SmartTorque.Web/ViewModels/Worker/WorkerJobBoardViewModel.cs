using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Worker;

public class WorkerJobCardViewModel
{
    public int JobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public PriorityLevel Priority { get; set; }
    public string PriorityLabel { get; set; } = string.Empty;
    public string PriorityHex { get; set; } = "#FFFFFF";
    public JobStatus Status { get; set; }
    public int Quantity { get; set; }
    public int QuantityCompleted { get; set; }
    public DateOnly? DueDate { get; set; }
    public string? AssignedWorkerId { get; set; }
    public string? AssignedWorkerName { get; set; }
    public bool IsAssignedToCurrentWorker { get; set; }
    public int? AssemblyLineId { get; set; }
    public string? AssemblyCode { get; set; }
    public string? AssemblyName { get; set; }
    public decimal EstimatedWorkload { get; set; } = 1.0m;
    public bool IsOpen => string.IsNullOrEmpty(AssignedWorkerId);
    public bool CanResume => Status == JobStatus.InProgress && IsAssignedToCurrentWorker;
    public bool CanStart => (Status == JobStatus.Assigned && IsAssignedToCurrentWorker) || (IsOpen && Status == JobStatus.Pending);
}

public class WorkerJobBoardViewModel
{
    public string WorkerName { get; set; } = string.Empty;
    public string WorkerId { get; set; } = string.Empty;
    public int? CurrentAssemblyLineId { get; set; }
    public string? CurrentAssemblyCode { get; set; }
    public string? CurrentAssemblyName { get; set; }
    public decimal WorkerActiveWorkload { get; set; }
    public List<SmartTorque.Core.Entities.AssemblyLine> AvailableAssemblyLines { get; set; } = new();
    public string ActiveFilter { get; set; } = "all";
    public int TotalCount { get; set; }
    public int MyJobsCount { get; set; }
    public int BlackCount { get; set; }
    public int OpenCount { get; set; }
    public List<WorkerJobCardViewModel> Jobs { get; set; } = new();
}