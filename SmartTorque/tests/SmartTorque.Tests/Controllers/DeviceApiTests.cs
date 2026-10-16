using System.Diagnostics;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers.Api;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class DeviceApiTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static (DeviceApiController controller, AppDbContext db, IDeviceService devService, ISessionService sesService) CreateController(AppDbContext db)
    {
        var eval = new ToleranceService();
        var devService = new DeviceService(db, eval);
        var sesService = new SessionService(db, eval);
        var hub = new FakeHubContext();
        var logger = NullLogger<DeviceApiController>.Instance;

        var controller = new DeviceApiController(devService, sesService, eval, hub, db, logger)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return (controller, db, devService, sesService);
    }

    [Fact]
    public async Task Register_ValidCredentials_ReturnsOkAndUpdatesLastSeen()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-01", "Torque Tool", InstrumentType.Torque, ExplicitApiKey: "secret_123"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "secret_123";

        var result = await controller.Register(new DeviceRegisterDto
        {
            TransmitterId = "ESP32-TW-01",
            Firmware = "v2.0.1",
            Instrument = InstrumentType.Torque
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var dev = await devService.GetByTransmitterIdAsync("ESP32-TW-01");
        dev.Should().NotBeNull();
        dev!.FirmwareVersion.Should().Be("v2.0.1");
        dev.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Register_InstrumentMismatch_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-01", "Torque Tool", InstrumentType.Torque, ExplicitApiKey: "secret_123"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "secret_123";

        var result = await controller.Register(new DeviceRegisterDto
        {
            TransmitterId = "ESP32-TW-01",
            Firmware = "v1.0",
            Instrument = InstrumentType.Vernier
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task Heartbeat_ValidKey_ReturnsOkAndUpdatesLastSeen()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-HB-01", "Vernier Caliper", InstrumentType.Vernier, ExplicitApiKey: "hb_key_456"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "hb_key_456";

        var result = await controller.Heartbeat(new DeviceHeartbeatDto
        {
            TransmitterId = "ESP32-HB-01",
            BatteryLevel = 92,
            SignalStrength = -58
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var dev = await devService.GetByTransmitterIdAsync("ESP32-HB-01");
        dev.Should().NotBeNull();
        dev!.LastSeenAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Heartbeat_MissingKey_ReturnsUnauthorized()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-HB-02", "Vernier", InstrumentType.Vernier, ExplicitApiKey: "hb_key_789"), "admin");

        var result = await controller.Heartbeat(new DeviceHeartbeatDto
        {
            TransmitterId = "ESP32-HB-02"
        }, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Heartbeat_InactiveDevice_ReturnsForbidden()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        var reg = await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-INACT", "Disabled Tool", InstrumentType.Torque, ExplicitApiKey: "key_inact"), "admin");
        await devService.SetActiveAsync(reg.Device.DeviceId, false);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "key_inact";

        var result = await controller.Heartbeat(new DeviceHeartbeatDto
        {
            TransmitterId = "ESP32-INACT"
        }, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task PostReading_InvalidUnitForTorque_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-02", "Torque Tool", InstrumentType.Torque, ExplicitApiKey: "torque_key"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "torque_key";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = "ESP32-TW-02",
            Value = 25.0m,
            Unit = "inches", // Invalid for torque
            IsStable = true
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task PostReading_FutureTimestamp_ReturnsBadRequest()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-03", "Torque Tool", InstrumentType.Torque, ExplicitApiKey: "torque_key"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "torque_key";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = "ESP32-TW-03",
            Value = 25.0m,
            Unit = "Nm",
            IsStable = true,
            Timestamp = DateTime.UtcNow.AddMinutes(15) // Future > 5 min
        }, CancellationToken.None);

        var badRequest = result.Should().BeOfType<BadRequestObjectResult>().Subject;
        badRequest.Value.Should().BeOfType<ProblemDetails>();
    }

    [Fact]
    public async Task PostReading_NoActiveSession_Returns202Accepted()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-04", "Torque Tool", InstrumentType.Torque, ExplicitApiKey: "torque_key"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "torque_key";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = "ESP32-TW-04",
            Value = 25.0m,
            Unit = "Nm",
            IsStable = true
        }, CancellationToken.None);

        var obj = result.Should().BeOfType<ObjectResult>().Subject;
        obj.StatusCode.Should().Be(StatusCodes.Status202Accepted);
    }

    private static async Task<(Device torqueDev, Device vernierDev, Session session)> SeedActiveSessionAsync(AppDbContext db, IDeviceService devService, ISessionService sesService)
    {
        var regTorque = await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-TW-ACTIVE", "Live Torque", InstrumentType.Torque, ExplicitApiKey: "tw_key_999"), "admin");
        var regVernier = await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-VC-ACTIVE", "Live Caliper", InstrumentType.Vernier, ExplicitApiKey: "vc_key_999"), "admin");

        var template = new Template
        {
            Name = "Active Test Template",
            TemplateCode = "TPL-ACT-01",
            Version = 1,
            IsLatest = true,
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        template.Steps.Add(new TemplateStep
        {
            SequenceNo = 1,
            Label = "Bolt 1 Tighten",
            Instrument = InstrumentType.Torque,
            Target = 30.0m,
            ToleranceMinus = 2.0m,
            TolerancePlus = 2.0m,
            ToleranceMode = ToleranceMode.Absolute,
            MaxRetries = 3
        });
        template.Steps.Add(new TemplateStep
        {
            SequenceNo = 2,
            Label = "Flange Gap Check",
            Instrument = InstrumentType.Vernier,
            Target = 5.0m,
            ToleranceMinus = 0.5m,
            TolerancePlus = 0.5m,
            ToleranceMode = ToleranceMode.Absolute,
            MaxRetries = 3
        });
        db.Templates.Add(template);

        var product = new Product
        {
            ProductCode = "PRD-ACT-01",
            ProductName = "Active Test Product",
            CreatedBy = "admin",
            CreatedAt = DateTime.UtcNow
        };
        var comp = new ProductComponent
        {
            ComponentName = "Housing",
            AssemblyOrder = 1,
            Quantity = 1,
            Template = template
        };
        product.Components.Add(comp);
        db.Products.Add(product);

        var job = new Job
        {
            JobNo = "JOB-ACT-001",
            ProductId = product.ProductId,
            Product = product,
            Quantity = 1,
            Priority = PriorityLevel.Yellow,
            Status = JobStatus.InProgress,
            AssignedWorkerId = "worker-act",
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var session = await sesService.StartOrResumeSessionAsync(job.JobId, "worker-act", comp.ProductComponentId);
        session.TorqueDeviceId = regTorque.Device.DeviceId;
        session.VernierDeviceId = regVernier.Device.DeviceId;
        await db.SaveChangesAsync();

        return (regTorque.Device, regVernier.Device, session);
    }

    [Fact]
    public async Task PostReading_LiveSampleStream_ReturnsOkWithLiveStatus()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (torqueDev, _, session) = await SeedActiveSessionAsync(db, devService, sesService);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "tw_key_999";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = torqueDev.TransmitterId,
            Value = 18.5m, // Ramp-up value
            Unit = "Nm",
            IsStable = false,
            SessionId = session.SessionId
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        // Assert that no session result record is persisted for non-stable live samples (Architecture §7.5)
        var count = await db.SessionResults.CountAsync(r => r.SessionId == session.SessionId);
        count.Should().Be(0);
    }

    [Fact]
    public async Task PostReading_UnexpectedInstrument_Returns409Conflict()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (_, vernierDev, session) = await SeedActiveSessionAsync(db, devService, sesService);

        // Step 1 expects Torque wrench, but Vernier sends a reading
        controller.HttpContext.Request.Headers["X-Api-Key"] = "vc_key_999";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = vernierDev.TransmitterId,
            Value = 5.0m,
            Unit = "mm",
            IsStable = true,
            SessionId = session.SessionId
        }, CancellationToken.None);

        var conflict = result.Should().BeOfType<ObjectResult>().Subject;
        conflict.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public async Task PostReading_ConfirmedPass_AdvancesStepAndReturnsOk()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (torqueDev, _, session) = await SeedActiveSessionAsync(db, devService, sesService);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "tw_key_999";

        var result = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = torqueDev.TransmitterId,
            Value = 30.5m, // In band [28 - 32]
            Unit = "Nm",
            IsStable = true,
            Seq = 1001,
            SessionId = session.SessionId
        }, CancellationToken.None);

        result.Should().BeOfType<OkObjectResult>();

        var updatedSession = await db.Sessions.FirstAsync(s => s.SessionId == session.SessionId);
        updatedSession.CurrentStepSequence.Should().Be(2); // Advanced to step 2

        var savedResult = await db.SessionResults.FirstAsync(r => r.SessionId == session.SessionId);
        savedResult.Result.Should().Be(StepResult.Pass);
        savedResult.ActualValue.Should().Be(30.5m);
        savedResult.DeviceSeq.Should().Be(1001);
    }

    [Fact]
    public async Task PostReading_Deduplication_ReturnsOkDuplicate()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (torqueDev, _, session) = await SeedActiveSessionAsync(db, devService, sesService);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "tw_key_999";

        // First transmission
        await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = torqueDev.TransmitterId,
            Value = 30.0m,
            Unit = "Nm",
            IsStable = true,
            Seq = 2002,
            SessionId = session.SessionId
        }, CancellationToken.None);

        // Replay of same seq
        var result2 = await controller.PostReading(new DeviceReadingDto
        {
            TransmitterId = torqueDev.TransmitterId,
            Value = 30.0m,
            Unit = "Nm",
            IsStable = true,
            Seq = 2002,
            SessionId = session.SessionId
        }, CancellationToken.None);

        result2.Should().BeOfType<OkObjectResult>();

        // Total saved results should remain 1
        var total = await db.SessionResults.CountAsync(r => r.SessionId == session.SessionId);
        total.Should().Be(1);
    }

    [Fact]
    public async Task GetSession_ActiveSessionExists_Returns200WithStepDetails()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (torqueDev, _, _) = await SeedActiveSessionAsync(db, devService, sesService);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "tw_key_999";

        var result = await controller.GetSession(torqueDev.TransmitterId, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var dto = okResult.Value.Should().BeOfType<DeviceSessionStepDto>().Subject;
        dto.StepLabel.Should().Be("Bolt 1 Tighten");
        dto.Instrument.Should().Be(InstrumentType.Torque);
        dto.Target.Should().Be(30.0m);
        dto.Unit.Should().Be("N·m");
    }

    [Fact]
    public async Task GetSession_NoActiveSession_Returns204NoContent()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, _) = CreateController(db);

        await devService.RegisterAsync(new RegisterDeviceRequest("ESP32-IDLE", "Idle Tool", InstrumentType.Torque, ExplicitApiKey: "idle_key"), "admin");

        controller.HttpContext.Request.Headers["X-Api-Key"] = "idle_key";

        var result = await controller.GetSession("ESP32-IDLE", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task IngestionLatency_Benchmark_ExecutesUnder30msPerReading()
    {
        using var db = CreateInMemoryDbContext();
        var (controller, _, devService, sesService) = CreateController(db);
        var (torqueDev, _, session) = await SeedActiveSessionAsync(db, devService, sesService);

        controller.HttpContext.Request.Headers["X-Api-Key"] = "tw_key_999";

        // Measure roundtrip latency of processing 20 live samples
        var sw = Stopwatch.StartNew();
        int iterations = 20;
        for (int i = 0; i < iterations; i++)
        {
            await controller.PostReading(new DeviceReadingDto
            {
                TransmitterId = torqueDev.TransmitterId,
                Value = 10.0m + i,
                Unit = "Nm",
                IsStable = false,
                SessionId = session.SessionId
            }, CancellationToken.None);
        }
        sw.Stop();

        double avgMs = sw.Elapsed.TotalMilliseconds / iterations;
        // P6.6 requirement: LAN latency well under 500 ms; our backend handles each sample in < 30ms
        avgMs.Should().BeLessThan(30.0);
    }
}