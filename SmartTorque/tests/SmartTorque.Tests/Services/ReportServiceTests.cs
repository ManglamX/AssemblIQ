using System.Text;

using ClosedXML.Excel;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

using SessionResultEntity = SmartTorque.Core.Entities.SessionResult;

namespace SmartTorque.Tests.Services;

public class ReportServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Job Job1, Job Job2, Device TorqueDev, Device VernierDev, ApplicationUser Worker1, ApplicationUser Worker2)> SeedReportDataAsync(AppDbContext db)
    {
        var worker1 = new ApplicationUser
        {
            Id = "w1",
            UserName = "worker1@smarttorque.local",
            Email = "worker1@smarttorque.local",
            FullName = "John Operator",
            EmployeeNo = "EMP-001",
            IsActive = true
        };
        var worker2 = new ApplicationUser
        {
            Id = "w2",
            UserName = "worker2@smarttorque.local",
            Email = "worker2@smarttorque.local",
            FullName = "Jane Specialist",
            EmployeeNo = "EMP-002",
            IsActive = true
        };
        db.Users.AddRange(worker1, worker2);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var torqueDev = new Device
        {
            TransmitterId = "TRQ-WRENCH-01",
            Name = "Atlas Copco Torque Wrench",
            InstrumentType = InstrumentType.Torque,
            Location = "Bench A",
            ApiKeyHash = "hash1",
            IsActive = true,
            LastSeenAt = now.AddSeconds(-10), // Online
            CalibrationDue = today.AddDays(90) // Current
        };
        var vernierDev = new Device
        {
            TransmitterId = "VER-CALIPER-01",
            Name = "Mitutoyo Vernier Caliper",
            InstrumentType = InstrumentType.Vernier,
            Location = "Bench B",
            ApiKeyHash = "hash2",
            IsActive = true,
            LastSeenAt = now.AddMinutes(-10), // Offline
            CalibrationDue = today.AddDays(-5) // Overdue
        };
        db.Devices.AddRange(torqueDev, vernierDev);

        var template = new Template
        {
            TemplateCode = "T-LOCKER-BASE",
            Name = "Locker Base Frame",
            Mode = TemplateMode.Mixed,
            Version = 1,
            IsLatest = true,
            Steps =
            {
                new TemplateStep
                {
                    SequenceNo = 1,
                    Label = "M8 Base Bolt",
                    Instrument = InstrumentType.Torque,
                    Target = 24.5m,
                    TolerancePlus = 1.5m,
                    ToleranceMinus = 1.5m,
                    ToleranceMode = ToleranceMode.Absolute
                },
                new TemplateStep
                {
                    SequenceNo = 2,
                    Label = "Gap Check",
                    Instrument = InstrumentType.Vernier,
                    Target = 12.0m,
                    TolerancePlus = 0.5m,
                    ToleranceMinus = 0.5m,
                    ToleranceMode = ToleranceMode.Absolute
                }
            }
        };
        db.Templates.Add(template);

        var product = new Product
        {
            ProductCode = "LK-6P",
            ProductName = "6-Panel Steel Locker",
            Description = "Standard industrial 6-panel locker",
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var comp = new ProductComponent
        {
            ProductId = product.ProductId,
            TemplateId = template.TemplateId,
            ComponentName = "Base Assembly",
            Quantity = 1,
            AssemblyOrder = 1
        };
        db.ProductComponents.Add(comp);
        await db.SaveChangesAsync();

        var job1 = new Job
        {
            JobNo = "JOB-2026-0001",
            ProductId = product.ProductId,
            ProductSerial = "SN-LOCKER-991",
            Quantity = 1,
            Priority = PriorityLevel.Black,
            DueDate = today.AddDays(1),
            Status = JobStatus.Completed,
            CreatedAt = now.AddHours(-3)
        };
        var job2 = new Job
        {
            JobNo = "JOB-2026-0002",
            ProductId = product.ProductId,
            ProductSerial = "SN-LOCKER-992",
            Quantity = 1,
            Priority = PriorityLevel.Red,
            DueDate = today.AddDays(2),
            Status = JobStatus.Pending,
            CreatedAt = now.AddHours(-1)
        };
        db.Jobs.AddRange(job1, job2);
        await db.SaveChangesAsync();

        // Session 1: Completed, Passed on first attempt (First-Pass Yield)
        var session1 = new Session
        {
            JobId = job1.JobId,
            ProductComponentId = comp.ProductComponentId,
            UnitNo = 1,
            ComponentInstanceNo = 1,
            WorkerId = worker1.Id,
            TorqueDeviceId = torqueDev.DeviceId,
            VernierDeviceId = vernierDev.DeviceId,
            StartTime = now.AddHours(-2),
            EndTime = now.AddHours(-2).AddMinutes(15),
            Status = SessionStatus.Completed,
            FinalResult = SmartTorque.Core.Enums.SessionResult.Pass,
            ReworkCount = 0
        };
        db.Sessions.Add(session1);
        await db.SaveChangesAsync();

        var step1 = template.Steps.First(s => s.SequenceNo == 1);
        var step2 = template.Steps.First(s => s.SequenceNo == 2);

        var res1_1 = new SessionResultEntity
        {
            SessionId = session1.SessionId,
            StepId = step1.TemplateStepId,
            InstrumentType = InstrumentType.Torque,
            DeviceId = torqueDev.DeviceId,
            AttemptNo = 1,
            TargetValue = 24.5m,
            TolerancePlus = 1.5m,
            ToleranceMinus = 1.5m,
            LowerLimit = 23.0m,
            UpperLimit = 26.0m,
            ActualValue = 24.8m,
            Result = StepResult.Pass,
            CapturedAt = now.AddHours(-2).AddMinutes(5)
        };
        var res1_2 = new SessionResultEntity
        {
            SessionId = session1.SessionId,
            StepId = step2.TemplateStepId,
            InstrumentType = InstrumentType.Vernier,
            DeviceId = vernierDev.DeviceId,
            AttemptNo = 1,
            TargetValue = 12.0m,
            TolerancePlus = 0.5m,
            ToleranceMinus = 0.5m,
            LowerLimit = 11.5m,
            UpperLimit = 12.5m,
            ActualValue = 12.1m,
            Result = StepResult.Pass,
            CapturedAt = now.AddHours(-2).AddMinutes(10)
        };

        // Session 2: Failed first attempt, retried and passed (Passed overall, but NOT FPY)
        var session2 = new Session
        {
            JobId = job1.JobId,
            ProductComponentId = comp.ProductComponentId,
            UnitNo = 1,
            ComponentInstanceNo = 2,
            WorkerId = worker2.Id,
            TorqueDeviceId = torqueDev.DeviceId,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(-1).AddMinutes(25),
            Status = SessionStatus.Completed,
            FinalResult = SmartTorque.Core.Enums.SessionResult.Pass,
            ReworkCount = 1
        };
        db.Sessions.Add(session2);
        await db.SaveChangesAsync();

        var res2_1_fail = new SessionResultEntity
        {
            SessionId = session2.SessionId,
            StepId = step1.TemplateStepId,
            InstrumentType = InstrumentType.Torque,
            DeviceId = torqueDev.DeviceId,
            AttemptNo = 1,
            TargetValue = 24.5m,
            TolerancePlus = 1.5m,
            ToleranceMinus = 1.5m,
            LowerLimit = 23.0m,
            UpperLimit = 26.0m,
            ActualValue = 21.0m, // FAIL
            Result = StepResult.Fail,
            CapturedAt = now.AddHours(-1).AddMinutes(5)
        };
        var res2_1_pass = new SessionResultEntity
        {
            SessionId = session2.SessionId,
            StepId = step1.TemplateStepId,
            InstrumentType = InstrumentType.Torque,
            DeviceId = torqueDev.DeviceId,
            AttemptNo = 2,
            TargetValue = 24.5m,
            TolerancePlus = 1.5m,
            ToleranceMinus = 1.5m,
            LowerLimit = 23.0m,
            UpperLimit = 26.0m,
            ActualValue = 24.6m, // PASS
            Result = StepResult.Pass,
            CapturedAt = now.AddHours(-1).AddMinutes(10)
        };

        db.SessionResults.AddRange(res1_1, res1_2, res2_1_fail, res2_1_pass);
        await db.SaveChangesAsync();

        return (job1, job2, torqueDev, vernierDev, worker1, worker2);
    }

    [Fact]
    public async Task GetSessionReportAsync_ReturnsAllSessions_AndAppliesFilters()
    {
        using var db = CreateInMemoryDbContext();
        var (job1, job2, torqueDev, _, worker1, worker2) = await SeedReportDataAsync(db);
        var service = new ReportService(db);

        // 1. Unfiltered
        var all = await service.GetSessionReportAsync(new SessionReportFilter());
        all.Should().HaveCount(2);

        // 2. Filter by Worker
        var workerFilter = await service.GetSessionReportAsync(new SessionReportFilter(WorkerId: worker1.Id));
        workerFilter.Should().HaveCount(1);
        workerFilter[0].WorkerName.Should().Be("John Operator");

        // 3. Filter by Device
        var devFilter = await service.GetSessionReportAsync(new SessionReportFilter(DeviceId: torqueDev.DeviceId));
        devFilter.Should().HaveCount(2);

        // 4. Filter by Search
        var searchFilter = await service.GetSessionReportAsync(new SessionReportFilter(Search: "SN-LOCKER-991"));
        searchFilter.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetSessionDetailAsync_ReturnsFullStepAuditHistory()
    {
        using var db = CreateInMemoryDbContext();
        var (job1, _, _, _, _, _) = await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var sessions = await service.GetSessionReportAsync(new SessionReportFilter());
        var targetSession = sessions.First(s => s.ReworkCount == 1);

        var detail = await service.GetSessionDetailAsync(targetSession.SessionId);
        detail.Should().NotBeNull();
        detail!.JobNo.Should().Be(job1.JobNo);
        detail.ReworkCount.Should().Be(1);
        detail.Steps.Should().HaveCount(2); // Attempt 1 (fail) and Attempt 2 (pass)

        var failStep = detail.Steps.First(s => s.AttemptNo == 1);
        failStep.Result.Should().Be(StepResult.Fail);
        failStep.ActualValue.Should().Be(21.0m);
        failStep.LowerLimit.Should().Be(23.0m);

        var passStep = detail.Steps.First(s => s.AttemptNo == 2);
        passStep.Result.Should().Be(StepResult.Pass);
        passStep.ActualValue.Should().Be(24.6m);
    }

    [Fact]
    public async Task GetTraceabilityReportAsync_MatchesBySerial_JobNo_AndDevice()
    {
        using var db = CreateInMemoryDbContext();
        var (job1, _, torqueDev, _, _, _) = await SeedReportDataAsync(db);
        var service = new ReportService(db);

        // 1. By Serial
        var bySerial = await service.GetTraceabilityReportAsync("SN-LOCKER-991");
        bySerial.Jobs.Should().HaveCount(1);
        bySerial.Jobs[0].JobNo.Should().Be(job1.JobNo);

        // 2. By Job No
        var byJobNo = await service.GetTraceabilityReportAsync(job1.JobNo);
        byJobNo.Jobs.Should().HaveCount(1);

        // 3. By Device Transmitter ID
        var byDevice = await service.GetTraceabilityReportAsync(torqueDev.TransmitterId);
        byDevice.DeviceReadings.Should().NotBeEmpty();
        byDevice.DeviceReadings.Count.Should().BeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public async Task GetPassFailSummaryAsync_CalculatesFPY_AndStepFailureRate()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var summary = await service.GetPassFailSummaryAsync(null, null);

        summary.TotalSessions.Should().Be(2);
        summary.PassedSessions.Should().Be(2);
        summary.FailedSessions.Should().Be(0);
        summary.OverallPassRate.Should().Be(100.0m);

        // First-Pass Yield: 1 out of 2 passed without rework/failure = 50%
        summary.FirstPassYield.Should().Be(50.0m);

        // Template breakdown
        summary.TemplateSummaries.Should().HaveCount(1);
        summary.TemplateSummaries[0].FirstPassYield.Should().Be(50.0m);

        // Step failure breakdown: Step 1 had 3 attempts, 1 failed = 33.33% failure rate
        summary.StepSummaries.Should().NotBeEmpty();
        var step1Summary = summary.StepSummaries.First(s => s.StepSequence == 1);
        step1Summary.TotalAttempts.Should().Be(3);
        step1Summary.FailedAttempts.Should().Be(1);
        step1Summary.FailureRate.Should().Be(33.33m);
    }

    [Fact]
    public async Task GetWorkerPerformanceReportAsync_CalculatesProductivityAndAverages()
    {
        using var db = CreateInMemoryDbContext();
        var (_, _, _, _, worker1, worker2) = await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var report = await service.GetWorkerPerformanceReportAsync(null, null);

        report.Should().HaveCount(2);

        var w1 = report.First(w => w.WorkerId == worker1.Id);
        w1.CompletedSessions.Should().Be(1);
        w1.PassedSessions.Should().Be(1);
        w1.FirstPassRate.Should().Be(100.0m);
        w1.AverageDurationMinutes.Should().Be(15.0);
        w1.TotalReworkCount.Should().Be(0);

        var w2 = report.First(w => w.WorkerId == worker2.Id);
        w2.CompletedSessions.Should().Be(1);
        w2.FirstPassRate.Should().Be(0.0m);
        w2.TotalReworkCount.Should().Be(1);
        w2.AverageDurationMinutes.Should().Be(25.0);
    }

    [Fact]
    public async Task GetDeviceUsageReportAsync_ComputesOnlineAndCalibrationStatus()
    {
        using var db = CreateInMemoryDbContext();
        var (_, _, torqueDev, vernierDev, _, _) = await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var report = await service.GetDeviceUsageReportAsync();

        report.Should().HaveCount(2);

        var trq = report.First(d => d.DeviceId == torqueDev.DeviceId);
        trq.IsOnline.Should().BeTrue();
        trq.CalibrationStatus.Should().Be("Current");
        trq.TotalReadings.Should().Be(3);

        var ver = report.First(d => d.DeviceId == vernierDev.DeviceId);
        ver.IsOnline.Should().BeFalse();
        ver.CalibrationStatus.Should().Be("Overdue");
        ver.TotalReadings.Should().Be(1);
    }

    [Fact]
    public async Task GetAdminDashboardStatsAsync_CalculatesKPIsAndBlackJobs()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var stats = await service.GetAdminDashboardStatsAsync();

        stats.TotalJobsToday.Should().Be(2);
        stats.CriticalBlackJobsWaiting.Should().Be(0); // Job 1 is Completed, Job 2 is Red (not Black)
        stats.OnlineDevicesCount.Should().Be(1);
        stats.OnlineTorqueCount.Should().Be(1);
        stats.OnlineVernierCount.Should().Be(0);
        stats.RecentSessions.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExportSessionsToExcelAsync_GeneratesValidWorkbook()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var bytes = await service.ExportSessionsToExcelAsync(new SessionReportFilter());

        bytes.Should().NotBeNullOrEmpty();

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var ws = workbook.Worksheet("Session Report");
        ws.Should().NotBeNull();
        ws.Cell(1, 1).GetString().Should().Contain("SmartTorque");
        ws.Cell(4, 1).GetString().Should().Be("Session ID");
        ws.Cell(5, 2).GetString().Should().Be("JOB-2026-0001");
    }

    [Fact]
    public async Task ExportSessionsToPdfAsync_GeneratesValidPdfBytes()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var bytes = await service.ExportSessionsToPdfAsync(new SessionReportFilter());

        bytes.Should().NotBeNullOrEmpty();
        var pdfHeader = Encoding.ASCII.GetString(bytes.Take(4).ToArray());
        pdfHeader.Should().Be("%PDF");
    }

    [Fact]
    public async Task ExportTraceabilityToExcelAsync_GeneratesValidWorkbook()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var bytes = await service.ExportTraceabilityToExcelAsync("SN-LOCKER-991");

        bytes.Should().NotBeNullOrEmpty();

        using var ms = new MemoryStream(bytes);
        using var workbook = new XLWorkbook(ms);
        var ws = workbook.Worksheet("Traceability");
        ws.Should().NotBeNull();
        ws.Cell(1, 1).GetString().Should().Contain("Traceability Audit Report");
    }

    [Fact]
    public async Task ExportTraceabilityToPdfAsync_GeneratesValidPdfBytes()
    {
        using var db = CreateInMemoryDbContext();
        await SeedReportDataAsync(db);
        var service = new ReportService(db);

        var bytes = await service.ExportTraceabilityToPdfAsync("SN-LOCKER-991");

        bytes.Should().NotBeNullOrEmpty();
        var pdfHeader = Encoding.ASCII.GetString(bytes.Take(4).ToArray());
        pdfHeader.Should().Be("%PDF");
    }
}