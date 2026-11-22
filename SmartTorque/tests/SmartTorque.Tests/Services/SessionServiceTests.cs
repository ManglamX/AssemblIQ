using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class SessionServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Job Job, Device TorqueDev, Device VernierDev)> SeedJobWithProductAsync(AppDbContext db)
    {
        var torqueDev = new Device
        {
            TransmitterId = "SIM-TORQUE-01",
            Name = "Sim Torque",
            InstrumentType = InstrumentType.Torque,
            ApiKeyHash = DeviceService.HashApiKey("key1"),
            IsActive = true
        };
        var vernierDev = new Device
        {
            TransmitterId = "SIM-VERNIER-01",
            Name = "Sim Vernier",
            InstrumentType = InstrumentType.Vernier,
            ApiKeyHash = DeviceService.HashApiKey("key2"),
            IsActive = true
        };
        db.Devices.AddRange(torqueDev, vernierDev);

        var template1 = new Template
        {
            TemplateCode = "T-FRAME",
            Name = "Frame Template",
            Mode = TemplateMode.Torque,
            IsLatest = true,
            Steps =
            {
                new TemplateStep
                {
                    SequenceNo = 1,
                    Label = "Anchor Bolt 1",
                    Instrument = InstrumentType.Torque,
                    Target = 25.0m,
                    TolerancePlus = 2.0m,
                    ToleranceMinus = 2.0m,
                    ToleranceMode = ToleranceMode.Absolute,
                    MaxRetries = 3
                },
                new TemplateStep
                {
                    SequenceNo = 2,
                    Label = "Anchor Bolt 2",
                    Instrument = InstrumentType.Torque,
                    Target = 25.0m,
                    TolerancePlus = 2.0m,
                    ToleranceMinus = 2.0m,
                    ToleranceMode = ToleranceMode.Absolute,
                    MaxRetries = 3
                }
            }
        };

        var product = new Product
        {
            ProductCode = "PRD-TEST",
            ProductName = "Test Product",
            IsActive = true,
            Components =
            {
                new ProductComponent
                {
                    Template = template1,
                    ComponentName = "Frame",
                    AssemblyOrder = 1,
                    Quantity = 1
                }
            }
        };
        db.Products.Add(product);

        var job = new Job
        {
            JobNo = "JOB-20261015-0001",
            Product = product,
            Quantity = 1,
            QuantityCompleted = 0,
            Priority = PriorityLevel.Red,
            Status = JobStatus.Pending
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        return (job, torqueDev, vernierDev);
    }

    [Fact]
    public async Task StartOrResumeSessionAsync_StartsJobAndCreatesSession()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, _, _) = await SeedJobWithProductAsync(db);

        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        session.Should().NotBeNull();
        session.JobId.Should().Be(job.JobId);
        session.WorkerId.Should().Be("worker-1");
        session.Status.Should().Be(SessionStatus.InProgress);
        session.CurrentStepSequence.Should().Be(1);

        var updatedJob = await db.Jobs.FindAsync(job.JobId);
        updatedJob!.Status.Should().Be(JobStatus.InProgress);
        updatedJob.AssignedWorkerId.Should().Be("worker-1");
    }

    [Fact]
    public async Task StartOrResumeSessionAsync_ResumesExistingActiveSession()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, _, _) = await SeedJobWithProductAsync(db);

        var session1 = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");
        var session2 = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        session1.SessionId.Should().Be(session2.SessionId);
    }

    [Fact]
    public async Task ProcessReadingAsync_InTolerance_PassesAndAdvancesStep()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        // Target is 25.0, tolerance [-2.0, +2.0] -> [23.0, 27.0]
        var cmd = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 101,
            Value: 25.5m,
            InstrumentType: InstrumentType.Torque
        );

        var result = await service.ProcessReadingAsync(cmd);

        result.Success.Should().BeTrue();
        result.IsStepPassed.Should().BeTrue();
        result.Result.Should().Be(StepResult.Pass);
        result.NextStepSequence.Should().Be(2);

        var updatedSession = await db.Sessions.FindAsync(session.SessionId);
        updatedSession!.CurrentStepSequence.Should().Be(2);
        updatedSession.Status.Should().Be(SessionStatus.InProgress);
    }

    [Fact]
    public async Task ProcessReadingAsync_OutTolerance_FailsAndIncrementsAttempt()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        // Value 20.0 is below 23.0 lower limit
        var cmd = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 102,
            Value: 20.0m,
            InstrumentType: InstrumentType.Torque
        );

        var result = await service.ProcessReadingAsync(cmd);

        result.Success.Should().BeTrue();
        result.IsStepPassed.Should().BeFalse();
        result.Result.Should().Be(StepResult.Fail);
        result.AttemptNo.Should().Be(1);
        result.IsExceededRetries.Should().BeFalse();

        var updatedSession = await db.Sessions.FindAsync(session.SessionId);
        updatedSession!.CurrentStepSequence.Should().Be(1); // Still on step 1
    }

    [Fact]
    public async Task ProcessReadingAsync_CompletesUnitAndJobWhenAllStepsPass()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        // Step 1: 25.0
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 201,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        // Step 2: 25.0 (last step of the component and product)
        var res2 = await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 202,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        res2.IsStepPassed.Should().BeTrue();
        res2.IsSessionCompleted.Should().BeTrue();
        res2.IsUnitCompleted.Should().BeTrue();
        res2.IsJobCompleted.Should().BeTrue();

        var completedJob = await db.Jobs.FindAsync(job.JobId);
        completedJob!.QuantityCompleted.Should().Be(1);
        completedJob.Status.Should().Be(JobStatus.Completed);
    }

    [Fact]
    public async Task ProcessReadingAsync_Deduplication_ReturnsIdempotentResult()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        var cmd = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 555,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        );

        var res1 = await service.ProcessReadingAsync(cmd);
        var res2 = await service.ProcessReadingAsync(cmd);

        res1.ResultId.Should().Be(res2.ResultId);
        var count = await db.SessionResults.CountAsync(r => r.DeviceSeq == 555);
        count.Should().Be(1);
    }

    [Fact]
    public async Task ProcessReadingAsync_UnlimitedRetries_And_ForceFail_TransitionsToFailAndRework()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        // Attempt 1: Fail (target is 25.0, value 10.0 is way below lower bound 23.0)
        var cmd1 = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 101,
            Value: 10.0m,
            InstrumentType: InstrumentType.Torque
        );
        var res1 = await service.ProcessReadingAsync(cmd1);
        res1.IsStepPassed.Should().BeFalse();
        res1.IsExceededRetries.Should().BeFalse();

        // Attempt 2: Fail - still unlimited retries
        var cmd2 = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 102,
            Value: 11.0m,
            InstrumentType: InstrumentType.Torque
        );
        var res2 = await service.ProcessReadingAsync(cmd2);
        res2.IsStepPassed.Should().BeFalse();
        res2.IsExceededRetries.Should().BeFalse();

        // Attempt 3: Fail - still unlimited retries, does not lock out
        var cmd3 = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 103,
            Value: 12.0m,
            InstrumentType: InstrumentType.Torque
        );
        var res3 = await service.ProcessReadingAsync(cmd3);
        res3.IsStepPassed.Should().BeFalse();
        res3.IsExceededRetries.Should().BeFalse();
        res3.AttemptNo.Should().Be(3);

        // Worker chooses to submit failed bolt for step 1 and advance
        var cmdFailStep1 = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: null,
            Value: 12.0m,
            InstrumentType: InstrumentType.Torque,
            ForceFail: true
        );
        var resFailStep1 = await service.ProcessReadingAsync(cmdFailStep1);
        resFailStep1.IsStepPassed.Should().BeFalse();
        resFailStep1.IsSessionCompleted.Should().BeFalse();
        resFailStep1.NextStepSequence.Should().Be(2);

        // Step 2 is completed by worker
        var cmdStep2 = new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 104,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        );
        var resStep2 = await service.ProcessReadingAsync(cmdStep2);
        resStep2.IsStepPassed.Should().BeTrue();
        resStep2.IsSessionCompleted.Should().BeTrue();

        var updatedSession = await db.Sessions.FindAsync(session.SessionId);
        updatedSession!.Status.Should().Be(SessionStatus.Completed);
        updatedSession.FinalResult.Should().Be(Core.Enums.SessionResult.Fail);

        var updatedJob = await db.Jobs.FindAsync(job.JobId);
        updatedJob!.Status.Should().Be(JobStatus.Rework);

        var jobUnit = await db.JobUnits.FirstOrDefaultAsync(u => u.JobId == job.JobId && u.UnitIndex == session.UnitNo);
        jobUnit.Should().NotBeNull();
        jobUnit!.Status.Should().Be(UnitAssemblyStatus.Failed);
    }

    [Fact]
    public async Task GetSessionSummaryAsync_ReturnsFullStepAuditAndStatus()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1");

        // Pass step 1
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 201,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        // Pass step 2
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 202,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        var summary = await service.GetSessionSummaryAsync(session.SessionId);
        summary.Should().NotBeNull();
        summary!.JobNo.Should().Be(job.JobNo);
        summary.FinalResult.Should().Be(Core.Enums.SessionResult.Pass);
        summary.Steps.Should().HaveCount(2);
        summary.Steps.All(s => s.Result == StepResult.Pass).Should().BeTrue();
    }

    [Fact]
    public async Task GetWorkerHistoryAsync_FiltersByPassOrFail()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-history-test");

        // Pass both steps
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 301,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 302,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        var allHistory = await service.GetWorkerHistoryAsync("worker-history-test");
        allHistory.Should().HaveCount(1);
        allHistory[0].FinalResult.Should().Be(Core.Enums.SessionResult.Pass);

        var passedOnly = await service.GetWorkerHistoryAsync("worker-history-test", "passed");
        passedOnly.Should().HaveCount(1);

        var failedOnly = await service.GetWorkerHistoryAsync("worker-history-test", "failed");
        failedOnly.Should().BeEmpty();
    }

    [Fact]
    public async Task GetWorkerProfileStatsAsync_ComputesYieldAndCounts()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var (job, torqueDev, _) = await SeedJobWithProductAsync(db);
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-profile-test");

        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 401,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));
        await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 402,
            Value: 25.0m,
            InstrumentType: InstrumentType.Torque
        ));

        var stats = await service.GetWorkerProfileStatsAsync("worker-profile-test");
        stats.Should().NotBeNull();
        stats.TotalSessions.Should().Be(1);
        stats.PassedSessions.Should().Be(1);
        stats.FailedSessions.Should().Be(0);
        stats.PassRate.Should().Be(100m);

        var charts = await service.GetWorkerProfileChartsAsync("worker-profile-test");
        charts.Should().NotBeNull();
        charts.OverallCpk.Should().NotBeNull();
    }

    [Fact]
    public async Task FullProductBuild_MaintainsSingleSessionAcrossAllComponentsAndCompletesUnit()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var service = new SessionService(db, evaluator);

        var torqueDev = new Device
        {
            TransmitterId = "MULTI-TORQUE-01",
            Name = "Multi Torque",
            InstrumentType = InstrumentType.Torque,
            IsActive = true
        };
        db.Devices.Add(torqueDev);

        var template1 = new Template
        {
            TemplateCode = "T-FRAME-MULTI",
            Name = "Frame Template",
            Mode = TemplateMode.Torque,
            IsLatest = true,
            Steps =
            {
                new TemplateStep { TemplateStepId = 801, SequenceNo = 1, Label = "Frame Bolt 1", Instrument = InstrumentType.Torque, Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = ToleranceMode.Absolute },
                new TemplateStep { TemplateStepId = 802, SequenceNo = 2, Label = "Frame Bolt 2", Instrument = InstrumentType.Torque, Target = 20.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = ToleranceMode.Absolute }
            }
        };

        var template2 = new Template
        {
            TemplateCode = "T-DOOR-MULTI",
            Name = "Door Template",
            Mode = TemplateMode.Torque,
            IsLatest = true,
            Steps =
            {
                new TemplateStep { TemplateStepId = 901, SequenceNo = 1, Label = "Door Bolt 1", Instrument = InstrumentType.Torque, Target = 30.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = ToleranceMode.Absolute },
                new TemplateStep { TemplateStepId = 902, SequenceNo = 2, Label = "Door Bolt 2", Instrument = InstrumentType.Torque, Target = 30.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m, ToleranceMode = ToleranceMode.Absolute }
            }
        };

        var product = new Product
        {
            ProductCode = "PRD-MULTI-COMP",
            ProductName = "Multi Component Product",
            IsActive = true,
            Components =
            {
                new ProductComponent { ProductComponentId = 81, ComponentName = "Frame", AssemblyOrder = 1, Template = template1 },
                new ProductComponent { ProductComponentId = 82, ComponentName = "Door", AssemblyOrder = 2, Template = template2 }
            }
        };
        db.Products.Add(product);

        var job = new Job
        {
            JobNo = "JOB-MULTI-001",
            Product = product,
            Quantity = 1,
            QuantityCompleted = 0,
            Priority = PriorityLevel.White,
            ScopeType = AllocationScopeType.FullProduct,
            Status = JobStatus.Pending
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        // 1. Start unit 1 assembly
        var session = await service.StartOrResumeSessionAsync(job.JobId, "worker-1", unitNo: 1);
        session.Should().NotBeNull();
        var originalSessionId = session.SessionId;
        session.ProductComponentId.Should().Be(81);
        session.CurrentStepSequence.Should().Be(1);

        // 2. Perform Component 1: Step 1 (Frame Bolt 1)
        var res1 = await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 1,
            Value: 20.0m,
            InstrumentType: InstrumentType.Torque
        ));
        res1.IsStepPassed.Should().BeTrue();
        res1.IsSessionCompleted.Should().BeFalse();
        res1.NextStepSequence.Should().Be(2);

        // 3. Perform Component 1: Step 2 (Frame Bolt 2 - end of Frame component)
        var res2 = await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: session.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 2,
            Value: 20.0m,
            InstrumentType: InstrumentType.Torque
        ));

        // VERIFY: Session did NOT end! It seamlessly transitions to Component 2 (Door)!
        res2.IsStepPassed.Should().BeTrue();
        res2.IsSessionCompleted.Should().BeFalse();
        res2.NextProductComponentId.Should().Be(82);

        var dbSessionMidway = await db.Sessions.FindAsync(originalSessionId);
        dbSessionMidway!.ProductComponentId.Should().Be(82);
        dbSessionMidway.CurrentStepSequence.Should().Be(1);
        dbSessionMidway.Status.Should().Be(SessionStatus.InProgress);

        // 4. Worker switches between panels or resumes: session ID stays exactly the same
        var switchedSession = await service.SwitchComponentAsync(job.JobId, 82, "worker-1", unitNo: 1);
        switchedSession.SessionId.Should().Be(originalSessionId);

        // 5. Perform Component 2: Step 1 (Door Bolt 1)
        var res3 = await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: switchedSession.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 3,
            Value: 30.0m,
            InstrumentType: InstrumentType.Torque
        ));
        res3.IsStepPassed.Should().BeTrue();
        res3.IsSessionCompleted.Should().BeFalse();

        // 6. Perform Component 2: Step 2 (Door Bolt 2 - final step of product)
        var res4 = await service.ProcessReadingAsync(new ProcessReadingCommand(
            SessionId: switchedSession.SessionId,
            DeviceId: torqueDev.DeviceId,
            TransmitterId: torqueDev.TransmitterId,
            DeviceSeq: 4,
            Value: 30.0m,
            InstrumentType: InstrumentType.Torque
        ));

        // VERIFY: Entire product is complete, unit is complete, in EXACTLY ONE SESSION!
        res4.IsStepPassed.Should().BeTrue();
        res4.IsSessionCompleted.Should().BeTrue();
        res4.IsUnitCompleted.Should().BeTrue();
        res4.IsJobCompleted.Should().BeTrue();

        // Verify database: ONLY ONE session exists for this PRD unit
        var totalSessionsOnUnit = await db.Sessions.CountAsync(s => s.JobId == job.JobId && s.UnitNo == 1);
        totalSessionsOnUnit.Should().Be(1);

        var finalSession = await db.Sessions.FindAsync(originalSessionId);
        finalSession!.Status.Should().Be(SessionStatus.Completed);
        finalSession.FinalResult.Should().Be(Core.Enums.SessionResult.Pass);

        // Verify summary contains all 4 steps across both components
        var summary = await service.GetSessionSummaryAsync(originalSessionId);
        summary.Should().NotBeNull();
        summary!.Steps.Should().HaveCount(4);
        summary.Steps.All(s => s.Result == StepResult.Pass).Should().BeTrue();
    }
}