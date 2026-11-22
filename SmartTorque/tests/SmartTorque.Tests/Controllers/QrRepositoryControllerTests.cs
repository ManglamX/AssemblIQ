using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.QrRepository;
using Xunit;

namespace SmartTorque.Tests.Controllers;

public class QrRepositoryControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedUnitsAsync(AppDbContext db)
    {
        var product = new Product
        {
            ProductId = 1,
            ProductCode = "PRD-QR-TEST",
            ProductName = "QR Test Safe",
            IsActive = true
        };
        db.Products.Add(product);

        var job = new Job
        {
            JobId = 10,
            JobNo = "JOB-QR-001",
            Product = product,
            ProductId = product.ProductId,
            Quantity = 3,
            QuantityCompleted = 1,
            Status = JobStatus.InProgress
        };
        db.Jobs.Add(job);

        var unitPending = new JobUnit
        {
            JobUnitId = 101,
            JobId = 10,
            UnitIndex = 1,
            TokenNo = "PRD-TOKEN-PENDING-001",
            Status = UnitAssemblyStatus.Pending
        };

        var unitInProgress = new JobUnit
        {
            JobUnitId = 102,
            JobId = 10,
            UnitIndex = 2,
            TokenNo = "PRD-TOKEN-INPROGRESS-002",
            Status = UnitAssemblyStatus.InProgress
        };

        var unitCompleted = new JobUnit
        {
            JobUnitId = 103,
            JobId = 10,
            UnitIndex = 3,
            TokenNo = "PRD-TOKEN-COMPLETED-003",
            Status = UnitAssemblyStatus.Completed
        };

        db.JobUnits.AddRange(unitPending, unitInProgress, unitCompleted);
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Index_WithoutStatusParam_DefaultsToPending()
    {
        using var db = CreateInMemoryDbContext();
        await SeedUnitsAsync(db);

        var fakeContext = new FakeUserContextService(new UserContextDto
        {
            UserId = "admin-1",
            RoleName = "Admin",
            IsMainAdmin = true
        });

        var hierarchy = new HierarchyService(db);
        var controller = new QrRepositoryController(
            db,
            fakeContext,
            hierarchy,
            NullLogger<QrRepositoryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // When navigating to the page with NO status parameter (default view)
        var result = await controller.Index(status: null);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<QrRepositoryViewModel>().Subject;

        // Default status must be "Pending"
        model.Status.Should().Be("Pending");

        // Units shown must be only Pending units
        model.Units.Should().HaveCount(1);
        model.Units[0].TokenNo.Should().Be("PRD-TOKEN-PENDING-001");
        model.Units[0].Status.Should().Be(UnitAssemblyStatus.Pending);

        // Global KPIs still show complete figures
        model.TotalCount.Should().Be(3);
        model.TotalPending.Should().Be(1);
        model.TotalInProgress.Should().Be(1);
        model.TotalPassed.Should().Be(1);
    }

    [Fact]
    public async Task Index_WithStatusAll_ReturnsAllUnits()
    {
        using var db = CreateInMemoryDbContext();
        await SeedUnitsAsync(db);

        var fakeContext = new FakeUserContextService(new UserContextDto
        {
            UserId = "admin-1",
            RoleName = "Admin",
            IsMainAdmin = true
        });

        var hierarchy = new HierarchyService(db);
        var controller = new QrRepositoryController(
            db,
            fakeContext,
            hierarchy,
            NullLogger<QrRepositoryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Explicit "all" status
        var result = await controller.Index(status: "all");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<QrRepositoryViewModel>().Subject;

        model.Units.Should().HaveCount(3);
    }

    [Fact]
    public async Task Index_WithStatusCompleted_ReturnsOnlyCompletedUnits()
    {
        using var db = CreateInMemoryDbContext();
        await SeedUnitsAsync(db);

        var fakeContext = new FakeUserContextService(new UserContextDto
        {
            UserId = "admin-1",
            RoleName = "Admin",
            IsMainAdmin = true
        });

        var hierarchy = new HierarchyService(db);
        var controller = new QrRepositoryController(
            db,
            fakeContext,
            hierarchy,
            NullLogger<QrRepositoryController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = await controller.Index(status: "Completed");

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<QrRepositoryViewModel>().Subject;

        model.Units.Should().HaveCount(1);
        model.Units[0].TokenNo.Should().Be("PRD-TOKEN-COMPLETED-003");
        model.Units[0].Status.Should().Be(UnitAssemblyStatus.Completed);
    }
}
