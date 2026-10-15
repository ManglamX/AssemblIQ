using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.User;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class UserApprovalTests
{
    private static (AppDbContext Db, UserManager<ApplicationUser> UserManager, RoleManager<IdentityRole> RoleManager) CreateIdentityContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new AppDbContext(options);

        var userStore = new UserStore<ApplicationUser>(db);
        var userManager = new UserManager<ApplicationUser>(
            userStore,
            Options.Create(new IdentityOptions()),
            new PasswordHasher<ApplicationUser>(),
            [],
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            null!,
            NullLogger<UserManager<ApplicationUser>>.Instance);

        var roleStore = new RoleStore<IdentityRole>(db);
        var roleManager = new RoleManager<IdentityRole>(
            roleStore,
            [],
            new UpperInvariantLookupNormalizer(),
            new IdentityErrorDescriber(),
            NullLogger<RoleManager<IdentityRole>>.Instance);

        return (db, userManager, roleManager);
    }

    [Fact]
    public async Task AdminApprove_ActivatesUserAndAssignsRole()
    {
        var (db, userManager, roleManager) = CreateIdentityContext();
        await roleManager.CreateAsync(new IdentityRole("Worker"));

        var user = new ApplicationUser
        {
            UserName = "newworker@test.com",
            Email = "newworker@test.com",
            FullName = "New Worker",
            RegistrationStatus = RegistrationStatus.Pending,
            RequestedRole = "Worker",
            IsActive = false
        };
        await userManager.CreateAsync(user);

        var controller = new UserController(userManager, roleManager, db, NullLogger<UserController>.Instance);
        var result = await controller.Approve(user.Id, "Worker");

        result.Should().BeOfType<RedirectToActionResult>();

        var updated = await userManager.FindByIdAsync(user.Id);
        updated!.RegistrationStatus.Should().Be(RegistrationStatus.Approved);
        updated.IsActive.Should().BeTrue();
        (await userManager.IsInRoleAsync(updated, "Worker")).Should().BeTrue();
    }

    [Fact]
    public async Task AdminReject_DeactivatesUserAndStoresReason()
    {
        var (db, userManager, roleManager) = CreateIdentityContext();

        var user = new ApplicationUser
        {
            UserName = "fakeuser@test.com",
            Email = "fakeuser@test.com",
            FullName = "Fake User",
            RegistrationStatus = RegistrationStatus.Pending,
            RequestedRole = "Worker",
            IsActive = false
        };
        await userManager.CreateAsync(user);

        var controller = new UserController(userManager, roleManager, db, NullLogger<UserController>.Instance);
        var result = await controller.Reject(user.Id, "Badge number does not match plant records.");

        result.Should().BeOfType<RedirectToActionResult>();

        var updated = await userManager.FindByIdAsync(user.Id);
        updated!.RegistrationStatus.Should().Be(RegistrationStatus.Rejected);
        updated.IsActive.Should().BeFalse();
        updated.RejectionReason.Should().Be("Badge number does not match plant records.");
    }

    [Fact]
    public async Task Index_FiltersPendingUsersCorrectly()
    {
        var (db, userManager, roleManager) = CreateIdentityContext();

        var u1 = new ApplicationUser { UserName = "u1@test.com", Email = "u1@test.com", FullName = "U1", RegistrationStatus = RegistrationStatus.Pending, IsActive = false };
        var u2 = new ApplicationUser { UserName = "u2@test.com", Email = "u2@test.com", FullName = "U2", RegistrationStatus = RegistrationStatus.Approved, IsActive = true };
        await userManager.CreateAsync(u1);
        await userManager.CreateAsync(u2);

        var controller = new UserController(userManager, roleManager, db, NullLogger<UserController>.Instance);
        var actionResult = await controller.Index("pending");

        var viewResult = actionResult.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<UserListViewModel>().Subject;

        vm.PendingCount.Should().Be(1);
        vm.Users.Should().HaveCount(1);
        vm.Users[0].Email.Should().Be("u1@test.com");
    }
}