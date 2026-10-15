using SmartTorque.Core.Enums;
using SmartTorque.Services;

namespace SmartTorque.Web.ViewModels.Bm;

public class WorkloadByItemViewModel
{
    public IReadOnlyList<ItemWorkloadSummary> Items { get; set; } = [];
    public string? Search { get; set; }

    public int TotalItemCodes => Items.Count;
    public int TotalQuantityAcrossAllBms => Items.Sum(i => i.TotalQuantityRequired);
    public int TotalQuantityCompleted => Items.Sum(i => i.TotalQuantityCompleted);
    public int TotalQuantityRemaining => Math.Max(0, TotalQuantityAcrossAllBms - TotalQuantityCompleted);
    public int TotalJobs => Items.Sum(i => i.TotalJobs);

    public static string PriorityLabel(PriorityLevel p) => (int)p switch
    {
        1 => "BLACK",
        2 => "RED",
        3 => "YELLOW",
        4 => "GREEN",
        5 => "BLUE",
        _ => "WHITE"
    };

    public static string PriorityIcon(PriorityLevel p) => (int)p switch
    {
        1 => "bi-circle-fill",
        2 => "bi-caret-up-fill",
        3 => "bi-diamond-fill",
        4 => "bi-square-fill",
        5 => "bi-hexagon-fill",
        _ => "bi-circle"
    };

    public static string StatusBadgeClass(JobStatus status) => status switch
    {
        JobStatus.Pending => "badge-pending",
        JobStatus.Assigned => "badge-assigned",
        JobStatus.InProgress => "badge-inprogress",
        JobStatus.Completed => "badge-completed",
        JobStatus.OnHold => "badge-onhold",
        JobStatus.Rework => "badge-rework",
        _ => "badge-cancelled"
    };
}