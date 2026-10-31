using System;
using System.Collections.Generic;

using Microsoft.AspNetCore.Mvc.Rendering;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.Bm;

public class WorkloadViewModel
{
    public List<Job> Jobs { get; set; } = new();
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }

    public int TotalPages =>
        PageSize <= 0 ? 1 : Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));

    public List<SelectListItem> Statuses { get; set; } = new();
    public List<SelectListItem> Workers { get; set; } = new();

    public bool IsPlantHead { get; set; }
    public int? DepartmentId { get; set; }
    public string? CurrentUserDepartmentName { get; set; }
    public List<SmartTorque.Services.HierarchyItemDto> AvailableDepartments { get; set; } = new();

    // Raw query-string values, so the filter bar round-trips exactly what was applied.
    public List<int> SelectedPriorities { get; set; } = new();
    public string? DueFrom { get; set; }
    public string? DueTo { get; set; }
    public string? Worker { get; set; }
    public string? Status { get; set; }

    /// <summary>Soft status badge per Design 5.5 (mirrors BmRowViewModel.StatusBadgeClass).</summary>
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