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

    [Fact]
    public async Task Index_ImmediateParentAdmin_OnlySeesScopedChildPendingUsers()
    {
        var (db, userManager, roleManager) = CreateIdentityContext();

        // Admin of Plant 1 (NodeId = 100)
        var plantAdmin = new ApplicationUser
        {
            UserName = "plant1admin@test.com",
            Email = "plant1admin@test.com",
            FullName = "Plant 1 Admin",
            IsMainAdmin = false,
            IsPlantHead = true,
            PlantId = 1
        };
        await userManager.CreateAsync(plantAdmin);

        // Plant 1 Node & Child Dept Node
        var plantNode = new HierarchyNode { NodeId = 100, Name = "Plant 1", NodeType = HierarchyNodeType.Plant, AdminUserId = plantAdmin.Id, IsActive = true };
        var deptNode1 = new HierarchyNode { NodeId = 101, Name = "Dept 1", NodeType = HierarchyNodeType.Department, ParentNodeId = 100, IsActive = true };
        // Other Plant 2 Node & Child Dept Node
        var otherPlantNode = new HierarchyNode { NodeId = 200, Name = "Plant 2", NodeType = HierarchyNodeType.Plant, AdminUserId = "other-admin", IsActive = true };
        var otherDeptNode = new HierarchyNode { NodeId = 201, Name = "Dept 2", NodeType = HierarchyNodeType.Department, ParentNodeId = 200, IsActive = true };

        db.HierarchyNodes.AddRange(plantNode, deptNode1, otherPlantNode, otherDeptNode);
        await db.SaveChangesAsync();

        // Worker requesting Dept 1 (under Plant 1)
        var userUnderPlant1 = new ApplicationUser
        {
            UserName = "worker1@test.com",
            Email = "worker1@test.com",
            FullName = "Worker 1",
            RegistrationStatus = RegistrationStatus.Pending,
            HierarchyNodeId = 101,
            IsActive = false
        };
        // Worker requesting Dept 2 (under Plant 2)
        var userUnderPlant2 = new ApplicationUser
        {
            UserName = "worker2@test.com",
            Email = "worker2@test.com",
            FullName = "Worker 2",
            RegistrationStatus = RegistrationStatus.Pending,
            HierarchyNodeId = 201,
            IsActive = false
        };

        await userManager.CreateAsync(userUnderPlant1);
        await userManager.CreateAsync(userUnderPlant2);

        // Simulate plantAdmin logged in
        var claims = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
        {
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, plantAdmin.Id),
            new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, plantAdmin.UserName!)
        }, "TestAuth"));

        var controller = new UserController(userManager, roleManager, db, NullLogger<UserController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = claims }
            }
        };

        var actionResult = await controller.Index("pending");
        var viewResult = actionResult.Should().BeOfType<ViewResult>().Subject;
        var vm = viewResult.Model.Should().BeOfType<UserListViewModel>().Subject;

        // Must ONLY see worker1 under Plant 1; worker2 under Plant 2 is excluded!
        vm.Users.Should().ContainSingle(u => u.Email == "worker1@test.com");
        vm.Users.Should().NotContain(u => u.Email == "worker2@test.com");
    }

    [Fact]
    public async Task Register_WorkerOrSupervisorWithoutDepartment_ReturnsModelError()
    {
        var (db, userManager, roleManager) = CreateIdentityContext();
        var hierarchyService = new SmartTorque.Services.HierarchyService(db);

        // AccountController with null signInManager (not needed for failed validation branch)
        var controller = new AccountController(
            null!,
            userManager,
            roleManager,
            hierarchyService,
            db,
            NullLogger<AccountController>.Instance);

        var model = new SmartTorque.Web.ViewModels.Account.RegisterViewModel
        {
            FullName = "John Doe",
            Email = "john@example.com",
            RequestedRole = "Worker",
            CompanyName = "Godrej",
            PlantName = "Plant 1",
            DepartmentName = "", // Empty department
            Password = "Password@123",
            ConfirmPassword = "Password@123"
        };

        var result = await controller.Register(model);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        controller.ModelState.ContainsKey("DepartmentName").Should().BeTrue();
        controller.ModelState["DepartmentName"]!.Errors[0].ErrorMessage.Should().Contain("Department");
    }
}