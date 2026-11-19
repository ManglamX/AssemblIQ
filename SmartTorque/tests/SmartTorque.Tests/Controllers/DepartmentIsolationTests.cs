using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.Product;
using Xunit;

namespace SmartTorque.Tests.Controllers;

public class FakeUserContextService : IUserContextService
{
    private readonly UserContextDto _dto;

    public FakeUserContextService(UserContextDto dto)
    {
        _dto = dto;
    }

    public Task<UserContextDto?> GetCurrentUserContextAsync(ClaimsPrincipal? principal, CancellationToken ct = default)
    {
        return Task.FromResult<UserContextDto?>(_dto);
    }

    public Task<UserContextDto?> GetUserContextByIdAsync(string userId, CancellationToken ct = default)
    {
        return Task.FromResult<UserContextDto?>(_dto);
    }
}

public class DepartmentIsolationTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public async Task ProductController_Index_ScopesProductsToDepartment_ForDeptAdmin()
    {
        using var db = CreateInMemoryDbContext();

        var deptEngg = new Department { DepartmentId = 5, Code = "ENGG", Name = "Engg Cell", PlantId = 1 };
        var deptFrfc = new Department { DepartmentId = 10, Code = "FRFC", Name = "frfc", PlantId = 1 };
        db.Departments.AddRange(deptEngg, deptFrfc);

        var p1 = new Product { ProductId = 1, ProductCode = "ENGG-01", ProductName = "Engg Prod 1", DepartmentId = 5, IsActive = true };
        var p2 = new Product { ProductId = 2, ProductCode = "ENGG-02", ProductName = "Engg Prod 2", DepartmentId = 5, IsActive = true };
        db.Products.AddRange(p1, p2);
        await db.SaveChangesAsync();

        var prodService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var tmplService = new TemplateService(db, imageService);

        var fakeContext = new FakeUserContextService(new UserContextDto
        {
            UserId = "user-frfc",
            Email = "frfc@godrej.com",
            RoleName = "Admin",
            IsMainAdmin = false,
            IsPlantHead = false,
            DepartmentId = 10,
            DepartmentName = "frfc"
        });

        var controller = new ProductController(
            prodService,
            tmplService,
            imageService,
            new ToleranceService(),
            new SettingsService(db),
            NullLogger<ProductController>.Instance,
            fakeContext,
            null)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act - Dept Admin visits /Product
        var result = await controller.Index(null, null, null, 1);

        // Assert - Should only see Dept 10 products (0 items), never Dept 5
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.Items.Should().BeEmpty();
        model.DepartmentId.Should().Be(10);
        model.IsPlantHead.Should().BeFalse();
    }

    [Fact]
    public async Task ProductController_Index_ShowsAllProducts_ForPlantHead()
    {
        using var db = CreateInMemoryDbContext();

        var deptEngg = new Department { DepartmentId = 5, Code = "ENGG", Name = "Engg Cell", PlantId = 1 };
        db.Departments.Add(deptEngg);

        var p1 = new Product { ProductId = 1, ProductCode = "ENGG-01", ProductName = "Engg Prod 1", DepartmentId = 5, IsActive = true };
        db.Products.Add(p1);
        await db.SaveChangesAsync();

        var prodService = new ProductService(db);
        var imageService = new ImageUploadService(Path.GetTempPath());
        var tmplService = new TemplateService(db, imageService);

        var fakeContext = new FakeUserContextService(new UserContextDto
        {
            UserId = "user-admin",
            Email = "admin@smarttorque.local",
            RoleName = "Admin",
            IsMainAdmin = true,
            IsPlantHead = true,
            DepartmentId = null
        });

        var controller = new ProductController(
            prodService,
            tmplService,
            imageService,
            new ToleranceService(),
            new SettingsService(db),
            NullLogger<ProductController>.Instance,
            fakeContext,
            null)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider()),
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        // Act
        var result = await controller.Index(null, null, null, 1);

        // Assert - Main Admin sees all products
        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<ProductListViewModel>().Subject;
        model.Items.Should().HaveCount(1);
        model.IsPlantHead.Should().BeTrue();
    }

    [Fact]
    public async Task AssemblyService_GetDashboardStatsAsync_FiltersByDepartment()
    {
        using var db = CreateInMemoryDbContext();

        var deptEngg = new Department { DepartmentId = 5, Code = "ENGG", Name = "Engg Cell", PlantId = 1 };
        var deptFrfc = new Department { DepartmentId = 10, Code = "FRFC", Name = "frfc", PlantId = 1 };
        db.Departments.AddRange(deptEngg, deptFrfc);

        var lineEngg = new AssemblyLine { AssemblyLineId = 1, AssemblyCode = "ENGG-LINE", AssemblyName = "Engg Line", Department = "Engg Cell", DepartmentId = 5, IsActive = true };
        var lineFrfc = new AssemblyLine { AssemblyLineId = 13, AssemblyCode = "FRFC-CELL", AssemblyName = "frfc cell", Department = "frfc", DepartmentId = 10, IsActive = true };
        db.AssemblyLines.AddRange(lineEngg, lineFrfc);

        // Product in Engg Cell
        var pEngg = new Product { ProductId = 1, ProductCode = "ENGG-01", ProductName = "Engg Prod", DepartmentId = 5, IsActive = true };
        db.Products.Add(pEngg);
        await db.SaveChangesAsync();

        var assemblyService = new AssemblyService(db, new NotificationService(), new PriorityService());

        // Act - Request dashboard stats for FRFC only
        var statsFrfc = await assemblyService.GetDashboardStatsAsync("frfc", CancellationToken.None);

        // Assert - Only 1 line (frfc cell), 0 mapped, 0 unmapped
        statsFrfc.TotalAssemblyLines.Should().Be(1);
        statsFrfc.UnmappedProductsCount.Should().Be(0);

        // Act - Request full plant stats (null department)
        var statsAll = await assemblyService.GetDashboardStatsAsync(null, CancellationToken.None);
        statsAll.TotalAssemblyLines.Should().Be(2);
        statsAll.UnmappedProductsCount.Should().Be(1);
    }

    [Fact]
    public async Task JobImportService_GetWorkloadByItemAsync_FiltersByDepartment()
    {
        using var db = CreateInMemoryDbContext();

        var pEngg = new Product { ProductId = 1, ProductCode = "ENGG-01", ProductName = "Engg Prod", DepartmentId = 5, IsActive = true };
        var pFrfc = new Product { ProductId = 2, ProductCode = "FRFC-01", ProductName = "FRFC Prod", DepartmentId = 10, IsActive = true };
        db.Products.AddRange(pEngg, pFrfc);

        var jobEngg = new Job { JobId = 1, JobNo = "JOB-ENGG", ProductId = 1, Quantity = 5, Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow };
        var jobFrfc = new Job { JobId = 2, JobNo = "JOB-FRFC", ProductId = 2, Quantity = 3, Status = JobStatus.Pending, CreatedAt = DateTime.UtcNow };
        db.Jobs.AddRange(jobEngg, jobFrfc);
        await db.SaveChangesAsync();

        var priorityService = new PriorityService();
        var jobImportService = new JobImportService(db, new NotificationService(), priorityService);

        // Act - Dept 10
        var workloadFrfc = await jobImportService.GetWorkloadByItemAsync(null, 10, CancellationToken.None);

        // Assert
        workloadFrfc.Should().HaveCount(1);
        workloadFrfc[0].ItemCode.Should().Be("FRFC-01");

        // Act - Dept 5
        var workloadEngg = await jobImportService.GetWorkloadByItemAsync(null, 5, CancellationToken.None);
        workloadEngg.Should().HaveCount(1);
        workloadEngg[0].ItemCode.Should().Be("ENGG-01");
    }

    [Fact]
    public async Task ReportService_GetAdminDashboardStatsAsync_ScopesRecentSessionsToDepartment()
    {
        using var db = CreateInMemoryDbContext();

        var deptEngg = new Department { DepartmentId = 5, Code = "ENGG", Name = "Engg Cell", PlantId = 1 };
        var deptSdlc = new Department { DepartmentId = 6, Code = "SDLC", Name = "SDLC", PlantId = 1 };
        db.Departments.AddRange(deptEngg, deptSdlc);

        var pEngg = new Product { ProductId = 1, ProductCode = "ENGG-01", ProductName = "Engg Prod", DepartmentId = 5, IsActive = true };
        var pSdlc = new Product { ProductId = 2, ProductCode = "SDLC-01", ProductName = "SDLC Prod", DepartmentId = 6, IsActive = true };
        db.Products.AddRange(pEngg, pSdlc);

        var tmpl = new Template { TemplateId = 1, TemplateCode = "T1", Name = "Panel Fastening", Mode = TemplateMode.Torque, CreatedBy = "test" };
        db.Templates.Add(tmpl);

        var compEngg = new ProductComponent { ProductComponentId = 1, ProductId = 1, TemplateId = 1, ComponentName = "Engg Component" };
        var compSdlc = new ProductComponent { ProductComponentId = 2, ProductId = 2, TemplateId = 1, ComponentName = "SDLC Component" };
        db.ProductComponents.AddRange(compEngg, compSdlc);

        var worker = new ApplicationUser { Id = "w1", UserName = "worker@test.com", FullName = "Worker 1" };
        db.Users.Add(worker);

        var jobEngg = new Job { JobId = 1, JobNo = "JOB-ENGG", ProductId = 1, Quantity = 1, Status = JobStatus.InProgress, CreatedAt = DateTime.UtcNow };
        var jobSdlc = new Job { JobId = 2, JobNo = "JOB-SDLC", ProductId = 2, Quantity = 1, Status = JobStatus.InProgress, CreatedAt = DateTime.UtcNow };
        db.Jobs.AddRange(jobEngg, jobSdlc);

        var sessionEngg = new Session
        {
            SessionId = 101,
            JobId = 1,
            ProductComponentId = 1,
            WorkerId = "w1",
            StartTime = DateTime.UtcNow.AddMinutes(-10),
            FinalResult = SmartTorque.Core.Enums.SessionResult.Pass
        };
        var sessionSdlc = new Session
        {
            SessionId = 102,
            JobId = 2,
            ProductComponentId = 2,
            WorkerId = "w1",
            StartTime = DateTime.UtcNow.AddMinutes(-5),
            FinalResult = SmartTorque.Core.Enums.SessionResult.Pass
        };
        db.Sessions.AddRange(sessionEngg, sessionSdlc);
        await db.SaveChangesAsync();

        var reportService = new ReportService(db);

        // Act - Dept Admin for SDLC (Dept 6)
        var statsSdlc = await reportService.GetAdminDashboardStatsAsync(6, CancellationToken.None);

        // Assert - RecentSessions should only show session 102, never 101
        statsSdlc.RecentSessions.Should().HaveCount(1);
        statsSdlc.RecentSessions[0].SessionId.Should().Be(102);

        // Act - Enterprise Admin / Plant Head (departmentId = null)
        var statsAll = await reportService.GetAdminDashboardStatsAsync(null, CancellationToken.None);
        statsAll.RecentSessions.Should().HaveCount(2);
    }
}
