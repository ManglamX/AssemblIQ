using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;

using System.Security.Claims;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class DeleteActionTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly DeviceService _deviceService;
    private readonly ApiKeyDirectoryService _apiKeyService;

    public DeleteActionTests()
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

        var roleStore = new RoleStore<IdentityRole>(_db);
        _roleManager = new RoleManager<IdentityRole>(
            roleStore,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        _deviceService = new DeviceService(_db);
        _apiKeyService = new ApiKeyDirectoryService(_db, _userManager);
    }

    public void Dispose()
    {
        _db.Dispose();
        _userManager.Dispose();
        _roleManager.Dispose();
    }

    [Fact]
    public async Task DeviceService_DeleteAsync_NoSessions_PermanentlyDeletesDevice()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("DEL-DEV-01", "Delete Me", InstrumentType.Torque), "admin");
        var deviceId = reg.Device.DeviceId;

        // Act
        var result = await _deviceService.DeleteAsync(deviceId);

        // Assert
        result.Success.Should().BeTrue();
        result.WasHardDeleted.Should().BeTrue();

        var deviceInDb = await _db.Devices.FindAsync(deviceId);
        deviceInDb.Should().BeNull();

        var keyInDb = await _db.ApiKeyDirectoryEntries.FirstOrDefaultAsync(k => k.ApiScope == "DEL-DEV-01");
        keyInDb.Should().BeNull();
    }

    [Fact]
    public async Task DeviceService_DeleteAsync_WithSessions_DeactivatesInsteadOfDeleting()
    {
        // Arrange
        var reg = await _deviceService.RegisterAsync(new RegisterDeviceRequest("DEL-DEV-02", "Session Device", InstrumentType.Torque), "admin");
        var deviceId = reg.Device.DeviceId;

        // Add a mock session referencing this device
        _db.Sessions.Add(new Session
        {
            SessionId = 999901,
            JobId = 1,
            ProductComponentId = 1,
            WorkerId = "some-worker-id",
            TorqueDeviceId = deviceId,
            Status = SessionStatus.Completed,
            StartTime = DateTime.UtcNow.AddMinutes(-30),
            EndTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _deviceService.DeleteAsync(deviceId);

        // Assert
        result.Success.Should().BeTrue();
        result.WasHardDeleted.Should().BeFalse();
        result.Message.Should().Contain("deactivated");

        var deviceInDb = await _db.Devices.FindAsync(deviceId);
        deviceInDb.Should().NotBeNull();
        deviceInDb!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ApiKeyDirectoryService_DeleteKeyAsync_DeletesKeyAndGrants()
    {
        // Arrange
        var key = await _apiKeyService.CreateKeyAsync("Test Delete Key", "SCOPE-01", "Desc", "RAWKEY123", "admin");
        _db.ApiKeyAccessGrants.Add(new ApiKeyAccessGrant
        {
            ApiKeyId = key.ApiKeyId,
            UserId = "user-123",
            Status = ApiKeyRequestStatus.Approved,
            RequestedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Act
        var result = await _apiKeyService.DeleteKeyAsync(key.ApiKeyId);

        // Assert
        result.Should().BeTrue();
        var keyInDb = await _db.ApiKeyDirectoryEntries.FindAsync(key.ApiKeyId);
        keyInDb.Should().BeNull();

        var grantsInDb = await _db.ApiKeyAccessGrants.Where(g => g.ApiKeyId == key.ApiKeyId).ToListAsync();
        grantsInDb.Should().BeEmpty();
    }

    [Fact]
    public async Task UserController_Delete_NoHistoricalSessions_DeletesUser()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "worker.del@smarttorque.local",
            Email = "worker.del@smarttorque.local",
            FullName = "Worker For Deletion",
            IsActive = true
        };
        await _userManager.CreateAsync(user);

        var controller = new UserController(
            _userManager,
            _roleManager,
            _db,
            NullLogger<UserController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "admin@smarttorque.local"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "TestAuth"))
                }
            }
        };

        // Act
        var result = await controller.Delete(user.Id);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        controller.TempData["Success"].Should().NotBeNull();
        var userInDb = await _userManager.FindByIdAsync(user.Id);
        userInDb.Should().BeNull();
    }

    [Fact]
    public async Task UserController_Delete_WithHistoricalSessions_DeactivatesUserAndWarns()
    {
        // Arrange
        var user = new ApplicationUser
        {
            UserName = "worker.sessions@smarttorque.local",
            Email = "worker.sessions@smarttorque.local",
            FullName = "Worker With Sessions",
            IsActive = true
        };
        await _userManager.CreateAsync(user);

        _db.Sessions.Add(new Session
        {
            SessionId = 999902,
            JobId = 1,
            ProductComponentId = 1,
            WorkerId = user.Id,
            Status = SessionStatus.Completed,
            StartTime = DateTime.UtcNow.AddMinutes(-40),
            EndTime = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        var controller = new UserController(
            _userManager,
            _roleManager,
            _db,
            NullLogger<UserController>.Instance)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(new[]
                    {
                        new Claim(ClaimTypes.Name, "admin@smarttorque.local"),
                        new Claim(ClaimTypes.Role, "Admin")
                    }, "TestAuth"))
                }
            }
        };

        // Act
        var result = await controller.Delete(user.Id);

        // Assert
        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be("Index");

        controller.TempData["Warning"].Should().NotBeNull();

        var userInDb = await _userManager.FindByIdAsync(user.Id);
        userInDb.Should().NotBeNull();
        userInDb!.IsActive.Should().BeFalse();
    }
}
