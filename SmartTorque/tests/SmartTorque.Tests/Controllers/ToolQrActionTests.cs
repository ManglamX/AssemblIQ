using System.Security.Claims;
using System.Text.Json;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Areas.Worker.Controllers;
using SmartTorque.Web.Controllers;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class ToolQrActionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly DeviceService _deviceService;
    private readonly QrCodeService _qrCodeService;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISessionService _sessionService;
    private readonly IHierarchyService _hierarchyService;
    private readonly IMemoryCache _cache;

    public ToolQrActionTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _db = new AppDbContext(options);

        var userStore = new UserStore<ApplicationUser>(_db);
        _userManager = new UserManager<ApplicationUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        _deviceService = new DeviceService(_db);
        _qrCodeService = new QrCodeService();
        _sessionService = new SessionService(_db, new ToleranceService());
        _hierarchyService = new HierarchyService(_db);
        _cache = new MemoryCache(new MemoryCacheOptions());
    }

    public void Dispose()
    {
        _db.Dispose();
        _userManager.Dispose();
        _cache.Dispose();
    }

    [Fact]
    public async Task DeviceController_QrCode_ValidId_ReturnsSvg()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("QR-DEV-01", "Torque Tool 1", InstrumentType.Torque), "admin");
        var controller = new DeviceController(_deviceService, _db, new FakeHubContext(), _qrCodeService);

        // Act
        var result = await controller.QrCode(reg.Device.DeviceId);

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("image/svg+xml");
        content.Content.Should().Contain("<svg");
    }

    [Fact]
    public void DeviceController_RawKeyQr_ValidKey_ReturnsSvg()
    {
        // Arrange
        var controller = new DeviceController(_deviceService, _db, new FakeHubContext(), _qrCodeService);

        // Act
        var result = controller.RawKeyQr("st_test_key_12345");

        // Assert
        var content = result.Should().BeOfType<ContentResult>().Subject;
        content.ContentType.Should().Be("image/svg+xml");
        content.Content.Should().Contain("<svg");
    }

    [Fact]
    public async Task DeviceController_PrintQr_ValidId_ReturnsViewWithDevice()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("QR-DEV-02", "Vernier Tool 2", InstrumentType.Vernier), "admin");
        var controller = new DeviceController(_deviceService, _db, new FakeHubContext(), _qrCodeService);

        // Act
        var result = await controller.PrintQr(reg.Device.DeviceId);

        // Assert
        var view = result.Should().BeOfType<ViewResult>().Subject;
        var model = view.Model.Should().BeOfType<Device>().Subject;
        model.TransmitterId.Should().Be("QR-DEV-02");
        view.ViewData["RawApiKey"].Should().NotBeNull();
        view.ViewData["QrSvg"].Should().NotBeNull();
    }

    [Fact]
    public async Task JobBoardController_LookupDeviceByQr_ValidRawApiKey_ReturnsMatchingDevice()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("ESP-TW-01", "Assembly Torque Wrench", InstrumentType.Torque), "admin");
        var rawKey = reg.RawApiKey;

        var controller = new JobBoardController(
            _db,
            _userManager,
            _sessionService,
            _hierarchyService,
            new FakeHubContext(),
            _cache);

        // Act
        var result = await controller.LookupDeviceByQr(rawKey);

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value));
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("transmitterId").GetString().Should().Be("ESP-TW-01");
        root.GetProperty("instrumentType").GetInt32().Should().Be(1); // InstrumentType.Torque
    }

    [Fact]
    public async Task JobBoardController_LookupDeviceByQr_TransmitterId_ReturnsMatchingDevice()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("VC-TOOL-99", "Digital Vernier", InstrumentType.Vernier), "admin");

        var controller = new JobBoardController(
            _db,
            _userManager,
            _sessionService,
            _hierarchyService,
            new FakeHubContext(),
            _cache);

        // Act
        var result = await controller.LookupDeviceByQr("VC-TOOL-99");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value));
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeTrue();
        root.GetProperty("transmitterId").GetString().Should().Be("VC-TOOL-99");
        root.GetProperty("instrumentType").GetInt32().Should().Be(2); // InstrumentType.Vernier
    }

    [Fact]
    public async Task JobBoardController_LookupDeviceByQr_InvalidCode_ReturnsFailure()
    {
        // Arrange
        var controller = new JobBoardController(
            _db,
            _userManager,
            _sessionService,
            _hierarchyService,
            new FakeHubContext(),
            _cache);

        // Act
        var result = await controller.LookupDeviceByQr("invalid_non_existent_key");

        // Assert
        var jsonResult = result.Should().BeOfType<JsonResult>().Subject;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(jsonResult.Value));
        var root = doc.RootElement;

        root.GetProperty("success").GetBoolean().Should().BeFalse();
    }
}
