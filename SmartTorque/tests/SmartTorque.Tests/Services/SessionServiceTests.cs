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
}