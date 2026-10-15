using SmartTorque.Core.Enums;

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
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewedBy { get; set; }
    public string? RejectionReason { get; set; }
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
}