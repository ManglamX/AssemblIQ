using Microsoft.AspNetCore.Mvc.Rendering;

using SmartTorque.Services;

namespace SmartTorque.Web.ViewModels;

public class SessionReportViewModel
{
    public SessionReportFilter Filter { get; set; } = new();
    public List<SessionReportItemDto> Sessions { get; set; } = new();
    public SelectList Workers { get; set; } = null!;
    public SelectList Products { get; set; } = null!;
    public SelectList Devices { get; set; } = null!;
    public int TotalSessions => Sessions.Count;
    public int PassedSessions => Sessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Pass);
    public int FailedSessions => Sessions.Count(s => s.FinalResult == SmartTorque.Core.Enums.SessionResult.Fail);
    public decimal PassRate => TotalSessions > 0 ? Math.Round((decimal)PassedSessions / TotalSessions * 100m, 1) : 0m;
}

public class TraceabilityReportViewModel
{
    public string Query { get; set; } = string.Empty;
    public TraceabilityReportDto Report { get; set; } = null!;
}

public class PassFailSummaryViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public PassFailSummaryDto Summary { get; set; } = null!;
}

public class WorkerPerformanceViewModel
{
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<WorkerPerformanceDto> Workers { get; set; } = new();
}

public class DeviceUsageViewModel
{
    public List<DeviceUsageDto> Devices { get; set; } = new();
    public int TotalDevices => Devices.Count;
    public int OnlineDevices => Devices.Count(d => d.IsOnline);
    public int OverdueCalibration => Devices.Count(d => d.CalibrationStatus == "Overdue");
}