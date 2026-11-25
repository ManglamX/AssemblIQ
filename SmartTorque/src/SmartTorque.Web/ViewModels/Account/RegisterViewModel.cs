using System.ComponentModel.DataAnnotations;

namespace SmartTorque.Web.ViewModels.Account;

public class RegisterViewModel
{
    [Required(ErrorMessage = "Full Name is required.")]
    [StringLength(200)]
    [Display(Name = "Full Name")]
    public string FullName { get; set; } = string.Empty;

    [Display(Name = "Employee / Badge ID")]
    [StringLength(50)]
    [RegularExpression(@"^\S*$", ErrorMessage = "Employee / Badge ID must not contain spaces.")]
    public string? EmployeeNo { get; set; }

    [EmailAddress(ErrorMessage = "Invalid email format.")]
    [Display(Name = "Work Email")]
    public string? Email { get; set; }

    [Display(Name = "Quick PIN (4 digits)")]
    [StringLength(8, ErrorMessage = "PIN cannot exceed 8 characters.")]
    public string? Pin { get; set; } = "1234";

    [Required(ErrorMessage = "Please select a requested role.")]
    [Display(Name = "Request Access As")]
    public string RequestedRole { get; set; } = "Worker";

    // Organizational Hierarchy - Typed with suggestion record matching
    [Display(Name = "Company")]
    [StringLength(100)]
    public string? CompanyName { get; set; }

    [Display(Name = "Plant")]
    [StringLength(100)]
    public string? PlantName { get; set; }

    [Display(Name = "Department")]
    [StringLength(100)]
    public string? DepartmentName { get; set; }

    public int? CompanyId { get; set; }
    public int? PlantId { get; set; }
    public int? DepartmentId { get; set; }

    public bool IsMissingDepartmentRequested { get; set; }

    [StringLength(100)]
    public string? MissingDepartmentName { get; set; }

    [Display(Name = "Line or Cell")]
    public int? AssemblyLineId { get; set; }

    [Display(Name = "Station or Subcell")]
    public int? WorkStationId { get; set; }

    [StringLength(100, MinimumLength = 8, ErrorMessage = "Password must be at least 8 characters.")]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
    [Display(Name = "Confirm Password")]
    public string? ConfirmPassword { get; set; }

    // Pre-loaded for simplified worker form
    public List<DepartmentOptionDto> AvailableDepartments { get; set; } = [];
    public List<LineOptionDto> AvailableLines { get; set; } = [];
}

public class DepartmentOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? PlantName { get; set; }
    public string? CompanyName { get; set; }
    public int? PlantId { get; set; }
    public int? CompanyId { get; set; }
}

public class LineOptionDto
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
}
