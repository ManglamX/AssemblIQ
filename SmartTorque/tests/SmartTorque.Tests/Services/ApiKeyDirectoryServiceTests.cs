using FluentAssertions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using Xunit;

namespace SmartTorque.Tests.Services;

public class ApiKeyDirectoryServiceTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApiKeyDirectoryService _service;

    public ApiKeyDirectoryServiceTests()
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

        _service = new ApiKeyDirectoryService(_db, _userManager);
    }

    public void Dispose()
    {
        _db.Dispose();
        _userManager.Dispose();
    }

    [Fact]
    public async Task IsMainAdminAsync_DistinguishesMainAdminFromRegularAdmin()
    {
        // Arrange
        var mainAdmin = new ApplicationUser
        {
            UserName = "admin@smarttorque.local",
            Email = "admin@smarttorque.local",
            IsMainAdmin = true
        };
        await _userManager.CreateAsync(mainAdmin);

        var regularAdmin = new ApplicationUser
        {
            UserName = "subadmin@test.com",
            Email = "subadmin@test.com",
            IsMainAdmin = false
        };
        await _userManager.CreateAsync(regularAdmin);

        // Act & Assert
        (await _service.IsMainAdminAsync(mainAdmin.Id)).Should().BeTrue();
        (await _service.IsMainAdminAsync(regularAdmin.Id)).Should().BeFalse();
    }

    [Fact]
    public async Task CreateKeyAsync_GeneratesEntryWithHash()
    {
        // Act
        var entry = await _service.CreateKeyAsync(
            "Test Key",
            "/api/test",
            "Test Description",
            "st_test_raw_key_12345",
            "admin@smarttorque.local");

        // Assert
        entry.ApiKeyId.Should().BeGreaterThan(0);
        entry.KeyName.Should().Be("Test Key");
        entry.RawApiKey.Should().Be("st_test_raw_key_12345");
        entry.ApiKeyHash.Should().NotBeNullOrWhiteSpace();
        entry.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task RequestAndApprovalWorkflow_PersistsGrantInTable()
    {
        // Arrange
        var mainAdmin = new ApplicationUser
        {
            UserName = "admin@smarttorque.local",
            Email = "admin@smarttorque.local",
            IsMainAdmin = true
        };
        await _userManager.CreateAsync(mainAdmin);

        var regularAdmin = new ApplicationUser
        {
            UserName = "dept_admin@smarttorque.local",
            Email = "dept_admin@smarttorque.local",
            IsMainAdmin = false
        };
        await _userManager.CreateAsync(regularAdmin);

        var key = await _service.CreateKeyAsync(
            "Readings API",
            "/api/readings",
            "Production readings ingestion",
            null,
            "admin@smarttorque.local");

        // Act 1: Regular admin requests access
        var request = await _service.RequestAccessAsync(
            key.ApiKeyId,
            regularAdmin.Id,
            "Need key to connect station 2 torque wrench");

        request.Status.Should().Be(ApiKeyRequestStatus.Pending);
        request.RequestReason.Should().Be("Need key to connect station 2 torque wrench");

        // Verify it appears in pending list for Main Admin
        var pending = await _service.GetPendingRequestsAsync();
        pending.Should().ContainSingle(p => p.GrantId == request.GrantId);

        // Before approval, regular admin does not have it in approved keys
        var approvedBefore = await _service.GetApprovedGrantsForUserAsync(regularAdmin.Id);
        approvedBefore.Should().BeEmpty();

        // Act 2: Main Admin approves access
        var approvedGrant = await _service.ApproveRequestAsync(request.GrantId, mainAdmin.Id);
        approvedGrant.Status.Should().Be(ApiKeyRequestStatus.Approved);
        approvedGrant.DecidedByUserId.Should().Be(mainAdmin.Id);
        approvedGrant.DecidedAt.Should().NotBeNull();

        // Act 3: Regular Admin now has persistent access in their approved table
        var approvedAfter = await _service.GetApprovedGrantsForUserAsync(regularAdmin.Id);
        approvedAfter.Should().ContainSingle();
        approvedAfter[0].ApiKey.KeyName.Should().Be("Readings API");
        approvedAfter[0].ApiKey.RawApiKey.Should().Be(key.RawApiKey);

        // Act 4: Main Admin revokes access
        var revokeResult = await _service.RevokeAccessAsync(request.GrantId, mainAdmin.Id);
        revokeResult.Should().BeTrue();

        var approvedAfterRevoke = await _service.GetApprovedGrantsForUserAsync(regularAdmin.Id);
        approvedAfterRevoke.Should().BeEmpty();
    }
}
