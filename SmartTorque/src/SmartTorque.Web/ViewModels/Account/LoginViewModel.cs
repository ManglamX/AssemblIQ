using System.ComponentModel.DataAnnotations;

namespace SmartTorque.Web.ViewModels.Account;

public class LoginViewModel
{
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    /// <summary>Active workers available for quick card login on shop floor tablets.</summary>
    public List<WorkerQuickLoginDto> ActiveWorkers { get; set; } = [];
}

public class WorkerQuickLoginDto
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? EmployeeNo { get; set; }
    public string? DepartmentName { get; set; }
    public string? AssemblyLineCode { get; set; }
    public string? StationCode { get; set; }
    public string Initials
    {
        get
        {
            if (string.IsNullOrWhiteSpace(FullName)) return "W";
            var parts = FullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 1) return parts[0].Substring(0, Math.Min(2, parts[0].Length)).ToUpperInvariant();
            return $"{parts[0][0]}{parts[^1][0]}".ToUpperInvariant();
        }
    }
}