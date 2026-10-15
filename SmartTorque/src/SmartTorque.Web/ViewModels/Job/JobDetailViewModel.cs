using Microsoft.AspNetCore.Mvc.Rendering;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Jobs;

/// <summary>
/// View model for the dedicated Job Detail page (P4.11 / Architecture §6.1).
/// </summary>
public class JobDetailViewModel
{
    public SmartTorque.Core.Entities.Job Job { get; set; } = null!;
    public IReadOnlyList<AuditLog> AuditLogs { get; set; } = [];
    public IReadOnlyList<SelectListItem> AvailableWorkers { get; set; } = [];

    public int JobId => Job.JobId;
    public string JobNo => Job.JobNo;
    public JobStatus Status => Job.Status;
    public PriorityLevel Priority => Job.Priority;
    public int PriorityNumber => (int)Priority;
    public int Quantity => Job.Quantity;
    public int QuantityCompleted => Job.QuantityCompleted;
    public int QuantityRemaining => Math.Max(0, Quantity - QuantityCompleted);
    public int ProgressPercent => Quantity > 0 ? (int)Math.Round((double)QuantityCompleted / Quantity * 100) : 0;
    public DateOnly? DueDate => Job.DueDate;
    public SmartTorque.Core.Entities.Product? Product => Job.Product;
    public ApplicationUser? AssignedWorker => Job.AssignedWorker;
    public BmRow? SourceBmRow => Job.SourceBmRow;

    public bool CanAssign => Status != JobStatus.Completed && Status != JobStatus.Cancelled;
    public bool CanHold => Status != JobStatus.Completed && Status != JobStatus.Cancelled && Status != JobStatus.OnHold;
    public bool CanResume => Status == JobStatus.OnHold;
    public bool CanCancel => Status != JobStatus.Completed && Status != JobStatus.Cancelled;
}