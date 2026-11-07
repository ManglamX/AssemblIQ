using Microsoft.AspNetCore.Mvc.Rendering;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.QrRepository;

public class QrRepositoryViewModel
{
    public List<QrUnitItemDto> Units { get; set; } = new();

    public int TotalCount { get; set; }
    public int TotalPassed { get; set; }
    public int TotalFailed { get; set; }
    public int TotalInProgress { get; set; }
    public int TotalPending { get; set; }
    public int TotalJobs { get; set; }

    public string? Search { get; set; }
    public int? DepartmentId { get; set; }
    public int? AssemblyLineId { get; set; }
    public string? Status { get; set; }

    public bool IsPlantHead { get; set; }
    public string? CurrentUserDepartmentName { get; set; }

    public List<SelectListItem> AvailableDepartments { get; set; } = new();
    public List<SelectListItem> AvailableLines { get; set; } = new();
}

public class QrUnitItemDto
{
    public int JobUnitId { get; set; }
    public int JobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public int UnitIndex { get; set; }
    public int TotalQuantity { get; set; }
    public string TokenNo { get; set; } = string.Empty;
    public string? ProductSerial { get; set; }

    public int ProductId { get; set; }
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;

    public int? AssemblyLineId { get; set; }
    public string AssemblyLineName { get; set; } = string.Empty;

    public UnitAssemblyStatus Status { get; set; }
    public bool HasSessions { get; set; }
    public bool IsQualityPassed { get; set; }
    public int PassedBolts { get; set; }
    public int TotalBolts { get; set; }

    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedByWorkerName { get; set; }
    public DateTime CreatedAt { get; set; }
}
