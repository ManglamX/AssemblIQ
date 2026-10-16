using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class FakeTempDataProvider : ITempDataProvider
{
    private readonly Dictionary<string, object> _values = new();
    public IDictionary<string, object> LoadTempData(HttpContext context) => _values;
    public void SaveTempData(HttpContext context, IDictionary<string, object> values) { }
}

public class DeviceControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static DeviceController CreateController(AppDbContext db, IDeviceService devService)
    {
        var hub = new FakeHubContext();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider());

        var controller = new DeviceController(devService, db, hub)
        {
            TempData = tempData,
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        return controller;
    }

    [Fact]
    public async Task Index_ReturnsViewWithDeviceListAndComputesOnlineStatus()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        var reg1 = await devService.RegisterAsync(new RegisterDeviceRequest("TW-01", "Online Torque", InstrumentType.Torque), "admin");
        var reg2 = await devService.RegisterAsync(new RegisterDeviceRequest("VC-01", "Offline Vernier", InstrumentType.Vernier), "admin");

        // Simulate reg1 having recent heartbeat, reg2 never seen
        await devService.UpdateHeartbeatAsync("TW-01");

        var result = await controller.Index();

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<List<DeviceListItemViewModel>>().Subject;

        model.Should().HaveCount(2);
        model.First(d => d.TransmitterId == "TW-01").IsOnline.Should().BeTrue();
        model.First(d => d.TransmitterId == "VC-01").IsOnline.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ValidModel_RegistersDeviceAndSetsTempDataApiKey()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        var model = new CreateDeviceViewModel
        {
            TransmitterId = "NEW-TW-99",
            Name = "New Torque Wrench",
            InstrumentType = InstrumentType.Torque,
            Location = "Line 2",
            CalibrationDue = DateOnly.FromDateTime(DateTime.UtcNow.AddMonths(6))
        };

        var result = await controller.Create(model);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        controller.TempData["NewApiKey"].Should().NotBeNull();
        controller.TempData["NewDeviceTransmitter"].Should().Be("NEW-TW-99");

        var created = await devService.GetByTransmitterIdAsync("NEW-TW-99");
        created.Should().NotBeNull();
        created!.Name.Should().Be("New Torque Wrench");
    }

    [Fact]
    public async Task Create_DuplicateTransmitterId_ReturnsViewWithModelError()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        await devService.RegisterAsync(new RegisterDeviceRequest("DUP-TW-01", "Existing Tool", InstrumentType.Torque), "admin");

        var model = new CreateDeviceViewModel
        {
            TransmitterId = "DUP-TW-01", // Duplicate
            Name = "Another Tool",
            InstrumentType = InstrumentType.Torque
        };

        var result = await controller.Create(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState.ContainsKey("TransmitterId").Should().BeTrue();
    }

    [Fact]
    public async Task Edit_ValidChanges_UpdatesDeviceAndRedirects()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        var reg = await devService.RegisterAsync(new RegisterDeviceRequest("EDIT-TW-01", "Old Name", InstrumentType.Torque, Location: "Old Loc"), "admin");

        var editModel = new EditDeviceViewModel
        {
            DeviceId = reg.Device.DeviceId,
            TransmitterId = "EDIT-TW-01",
            Name = "Updated Name",
            Location = "Station 9"
        };

        var result = await controller.Edit(reg.Device.DeviceId, editModel);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        var updated = await devService.GetByIdAsync(reg.Device.DeviceId);
        updated.Should().NotBeNull();
        updated!.Name.Should().Be("Updated Name");
        updated.Location.Should().Be("Station 9");
    }

    [Fact]
    public async Task ReissueKey_ValidId_GeneratesNewKeyAndStoresInTempData()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        var reg = await devService.RegisterAsync(new RegisterDeviceRequest("REISSUE-01", "Key Tool", InstrumentType.Torque), "admin");
        var oldHash = reg.Device.ApiKeyHash;

        var result = await controller.ReissueKey(reg.Device.DeviceId);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        controller.TempData["NewApiKey"].Should().NotBeNull();

        var updated = await devService.GetByIdAsync(reg.Device.DeviceId);
        updated!.ApiKeyHash.Should().NotBe(oldHash);
    }

    [Fact]
    public async Task ToggleActive_InvertsActiveStatus()
    {
        using var db = CreateInMemoryDbContext();
        var devService = new DeviceService(db);
        var controller = CreateController(db, devService);

        var reg = await devService.RegisterAsync(new RegisterDeviceRequest("TOGGLE-01", "Toggle Tool", InstrumentType.Torque), "admin");
        reg.Device.IsActive.Should().BeTrue();

        var result = await controller.ToggleActive(reg.Device.DeviceId);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        var toggled = await devService.GetByIdAsync(reg.Device.DeviceId);
        toggled!.IsActive.Should().BeFalse();
    }
}