using System.Security.Claims;

using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.Settings;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class SettingsControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static SettingsController CreateController(AppDbContext db)
    {
        var tempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider());

        return new SettingsController(new SettingsService(db), NullLogger<SettingsController>.Instance)
        {
            TempData = tempData,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        [new Claim(ClaimTypes.Name, "admin@factory.com")], "Test"))
                }
            }
        };
    }

    private const string DefaultHeaderMapJson =
        @"{ ""ProductCode"": ""Item Code"", ""Quantity"": ""Qty"", ""DueDate"": ""Due Date"", ""Remarks"": ""Remarks"", ""SalesOrder"": ""Sales Order"", ""PONo"": ""PO No"" }";

    // ================================================================
    // GET /Settings
    // ================================================================

    [Fact]
    public async Task Index_Get_RendersFormWithCurrentValues()
    {
        using var db = CreateInMemoryDbContext();
        db.Settings.Add(new Core.Entities.Setting { Key = "StableHoldMs", Value = "800" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);

        var result = await controller.Index(CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Which;
        var model = view.Model.Should().BeOfType<SettingsIndexViewModel>().Which;

        model.DeviceOfflineSeconds.Should().Be("30"); // VM default when row missing
        model.StableHoldMs.Should().Be("800"); // hydrated from the store
        controller.ViewData["ActiveNav"].Should().Be("Settings");
    }

    // ================================================================
    // POST /Settings — success path
    // ================================================================

    [Fact]
    public async Task Index_Post_Valid_SavesAndRedirects()
    {
        using var db = CreateInMemoryDbContext();
        db.Settings.Add(new Core.Entities.Setting { Key = "DeviceOfflineSeconds", Value = "30" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new SettingsIndexViewModel
        {
            DeviceOfflineSeconds = "45",
            ImageMaxMb = "8",
            MaxRetriesDefault = "4",
            BmHeaderMap = DefaultHeaderMapJson
        };

        var result = await controller.Index(model, CancellationToken.None);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.ActionName.Should().Be("Index");

        (await db.Settings.SingleAsync(s => s.Key == "DeviceOfflineSeconds"))
            .Value.Should().Be("45");
        (await db.Settings.SingleAsync(s => s.Key == "ImageMaxMb"))
            .Value.Should().Be("8");
        (await db.Settings.CountAsync(s => s.Key == "BmHeaderMap"))
            .Should().Be(1);
        (await db.Settings.SingleAsync(s => s.Key == "ImageMaxMb"))
            .UpdatedBy.Should().Be("admin@factory.com");

        controller.TempData["Success"].Should().NotBeNull();
    }

    // ================================================================
    // POST /Settings — invalid paths
    // ================================================================

    [Fact]
    public async Task Index_Post_OutOfRange_DoesNotSave_AndShowsError()
    {
        using var db = CreateInMemoryDbContext();
        db.Settings.Add(new Core.Entities.Setting { Key = "DeviceOfflineSeconds", Value = "30" });
        await db.SaveChangesAsync();

        var controller = CreateController(db);
        var model = new SettingsIndexViewModel { DeviceOfflineSeconds = "0" };

        var result = await controller.Index(model, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Which;
        view.Model.Should().BeOfType<SettingsIndexViewModel>()
            .Which.DeviceOfflineSeconds.Should().Be("0"); // admin input preserved

        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[string.Empty].Should().NotBeNull(); // service message on summary

        (await db.Settings.SingleAsync(s => s.Key == "DeviceOfflineSeconds"))
            .Value.Should().Be("30"); // untouched
    }

    [Fact]
    public async Task Index_Post_NonNumeric_IsRejectedBeforeSaving()
    {
        using var db = CreateInMemoryDbContext();

        var controller = CreateController(db);
        var model = new SettingsIndexViewModel { MaxRetriesDefault = "many" };

        var result = await controller.Index(model, CancellationToken.None);

        var view = result.Should().BeOfType<ViewResult>().Which;
        view.Model.Should().BeOfType<SettingsIndexViewModel>()
            .Which.MaxRetriesDefault.Should().Be("many");

        controller.ModelState.IsValid.Should().BeFalse();
        controller.ModelState[nameof(SettingsIndexViewModel.MaxRetriesDefault)].Should().NotBeNull();

        db.Settings.Should().BeEmpty();
    }
}