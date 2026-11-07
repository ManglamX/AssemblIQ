namespace SmartTorque.Web.ViewModels.Jobs;

public class PrintUnitQrsViewModel
{
    public string Title { get; set; } = "Unit QR Code Labels";
    public string Subtitle { get; set; } = string.Empty;
    public int TotalJobs { get; set; } = 1;
    public int TotalUnits => Items.Count;
    public List<PrintUnitItemViewModel> Items { get; set; } = new();
}

public class PrintUnitItemViewModel
{
    public int JobId { get; set; }
    public string JobNo { get; set; } = string.Empty;
    public string ProductCode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int UnitIndex { get; set; }
    public int TotalQuantity { get; set; }
    public string TokenNo { get; set; } = string.Empty;
    public string? ProductSerial { get; set; }
}
