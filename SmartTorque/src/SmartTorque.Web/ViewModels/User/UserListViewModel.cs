using SmartTorque.Core.Enums;
using SmartTorque.Services;

namespace SmartTorque.Web.ViewModels.User;

public class UserCardViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeNo { get; set; }
    public RegistrationStatus RegistrationStatus { get; set; }
    public string RequestedRole { get; set; } = "Worker";
    public string CurrentRole { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsMainAdmin { get; set; }
    public bool IsPlantHead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }

    // Hierarchy Info
    public int? CompanyId { get; set; }
    public string? CompanyName { get; set; }
    public int? PlantId { get; set; }
    public string? PlantName { get; set; }
    public int? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public int? CurrentAssemblyLineId { get; set; }
    public string? AssemblyLineName { get; set; }
    public int? WorkStationId { get; set; }
    public string? WorkStationName { get; set; }
}

public class UserListViewModel
{
    public string ActiveFilter { get; set; } = "pending";
    public int PendingCount { get; set; }
    public int WorkersCount { get; set; }
    public int AdminsCount { get; set; }
    public int RejectedCount { get; set; }
    public int TotalCount { get; set; }
    public List<UserCardViewModel> Users { get; set; } = new();

    // Department Scoping
    public int? SelectedDepartmentId { get; set; }
    public bool IsPlantHead { get; set; }
    public string? CurrentUserDepartmentName { get; set; }
    public List<HierarchyItemDto> AvailableDepartments { get; set; } = new();
}
