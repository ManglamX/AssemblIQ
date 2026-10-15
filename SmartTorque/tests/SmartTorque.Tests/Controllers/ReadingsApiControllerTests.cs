using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers.Api;
using SmartTorque.Web.Hubs;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class FakeClientProxy : IClientProxy
{
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeHubClients : IHubClients
{
    public IClientProxy All => new FakeClientProxy();
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
    public IClientProxy Client(string connectionId) => new FakeClientProxy();
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => new FakeClientProxy();
    public IClientProxy Group(string groupName) => new FakeClientProxy();
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => new FakeClientProxy();
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new FakeClientProxy();
    public IClientProxy User(string userId) => new FakeClientProxy();
    public IClientProxy Users(IReadOnlyList<string> userIds) => new FakeClientProxy();
}

public class FakeGroupManager : IGroupManager
{
    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;
}

public class FakeHubContext : IHubContext<LiveReadingHub>
{
    public IHubClients Clients => new FakeHubClients();
    public IGroupManager Groups => new FakeGroupManager();
}

public class ReadingsApiControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task PostReading_MissingCredentials_ReturnsUnauthorized()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var sesService = new SessionService(db, new ToleranceService());
        var hubContext = new FakeHubContext();

        var controller = new ReadingsApiController(devService, sesService, hubContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.PostReading(new DeviceReadingRequest
        {
            Value = 25.0m
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PostReading_InvalidApiKey_ReturnsUnauthorized()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var sesService = new SessionService(db, new ToleranceService());
        var hubContext = new FakeHubContext();

        await devService.RegisterAsync(
            new RegisterDeviceRequest("TORQUE-01", "Torque 1", InstrumentType.Torque, ExplicitApiKey: "correct_key"),
            "admin"
        );

        var controller = new ReadingsApiController(devService, sesService, hubContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.PostReading(new DeviceReadingRequest
        {
            TransmitterId = "TORQUE-01",
            ApiKey = "wrong_key",
            Value = 25.0m
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PostReading_ValidCredentialsWithActiveSession_ReturnsOk()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var sesService = new SessionService(db, new ToleranceService());
        var hubContext = new FakeHubContext();

        var reg = await devService.RegisterAsync(
            new RegisterDeviceRequest("SIM-TORQUE-01", "Sim Torque", InstrumentType.Torque, ExplicitApiKey: "test_key"),
            "admin"
        );

        var template = new Template
        {
            TemplateCode = "T-01",
            Name = "Template 1",
            Mode = TemplateMode.Torque,
            Steps =
            {
                new TemplateStep { SequenceNo = 1, Label = "Bolt 1", Instrument = InstrumentType.Torque, Target = 25.0m, TolerancePlus = 2.0m, ToleranceMinus = 2.0m }
            }
        };

        var product = new Product
        {
            ProductCode = "P-01",
            ProductName = "Product 1",
            Components = { new ProductComponent { Template = template, ComponentName = "Comp 1", AssemblyOrder = 1 } }
        };
        db.Products.Add(product);

        var job = new Job
        {
            JobNo = "JOB-TEST-01",
            Product = product,
            Quantity = 1,
            Status = JobStatus.InProgress,
            AssignedWorkerId = "worker-1"
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var session = await sesService.StartOrResumeSessionAsync(job.JobId, "worker-1");

        var controller = new ReadingsApiController(devService, sesService, hubContext)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        controller.Request.Headers["X-Transmitter-Id"] = "SIM-TORQUE-01";
        controller.Request.Headers["X-Api-Key"] = "test_key";

        var result = await controller.PostReading(new DeviceReadingRequest
        {
            SessionId = session.SessionId,
            Value = 25.0m,
            DeviceSeq = 1
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();
        var ok = result as OkObjectResult;
        ok!.Value.Should().BeOfType<ReadingEvaluationResult>();
        var eval = ok.Value as ReadingEvaluationResult;
        eval!.IsStepPassed.Should().BeTrue();
    }
}