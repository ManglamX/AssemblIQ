using System.ComponentModel.DataAnnotations;

using SmartTorque.Core.Enums;

namespace SmartTorque.Web.ViewModels.ColourMap;

public class ColourMapListViewModel
{
    public List<ColourMapRowViewModel> Items { get; set; } = [];

    /// <summary>Form for create, or for the entry being edited.</summary>
    public ColourMapFormViewModel Form { get; set; } = new();

    public bool HasItems => Items.Count > 0;
}

public class ColourMapRowViewModel
{
    public int MapId { get; set; }

    public string ColourHex { get; set; } = string.Empty;

    public PriorityLevel Priority { get; set; }

    /// <summary>Used for the badge modifier class and the select value.</summary>
    public int PriorityNumber => (int)Priority;

    public string Label { get; set; } = string.Empty;

    public int MatchTolerance { get; set; }

    public bool IsActive { get; set; }
}

public class ColourMapFormViewModel
{
    /// <summary>Set when editing an existing shade.</summary>
    public int? EditingMapId { get; set; }

    [Required(ErrorMessage = "Colour is required")]
    [RegularExpression(@"^#[0-9A-Fa-f]{6}$", ErrorMessage = "Colour must be a hex value such as #000000")]
    [Display(Name = "Colour")]
    public string ColourHex { get; set; } = string.Empty;

    [Range(1, 6, ErrorMessage = "Priority must be 1 (Black) to 6 (White)")]
    [Display(Name = "Priority")]
    public int Priority { get; set; } = 1;

    [Required(ErrorMessage = "Label is required")]
    [MaxLength(30, ErrorMessage = "Label must be 30 characters or fewer")]
    [Display(Name = "Label")]
    public string Label { get; set; } = string.Empty;

    [Range(1, 441, ErrorMessage = "Match tolerance must be between 1 and 441")]
    [Display(Name = "Match tolerance (RGB distance)")]
    public int MatchTolerance { get; set; } = 40;

    public bool IsEdit => EditingMapId.HasValue;

    /// <summary>True when the colour box can be previewed (valid hex).</summary>
    public bool HasColour => System.Text.RegularExpressions.Regex.IsMatch(ColourHex ?? string.Empty, "^#[0-9A-Fa-f]{6}$");
}