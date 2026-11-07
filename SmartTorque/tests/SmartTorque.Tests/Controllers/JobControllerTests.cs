using FluentAssertions;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using SmartTorque.Web.Controllers;
using SmartTorque.Web.ViewModels.Jobs;

using Xunit;

namespace SmartTorque.Tests.Controllers;

public class JobControllerTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static UserManager<ApplicationUser> CreateUserManager(AppDbContext db)
    {
        var store = new Microsoft.AspNetCore.Identity.EntityFrameworkCore.UserStore<ApplicationUser>(db);
        var options = Options.Create(new IdentityOptions());
        var passwordHasher = new PasswordHasher<ApplicationUser>();
        var userValidators = new List<IUserValidator<ApplicationUser>> { new UserValidator<ApplicationUser>() };
        var passwordValidators = new List<IPasswordValidator<ApplicationUser>> { new PasswordValidator<ApplicationUser>() };
        var keyNormalizer = new UpperInvariantLookupNormalizer();
        var errors = new IdentityErrorDescriber();
        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();

        return new UserManager<ApplicationUser>(
            store, options, passwordHasher, userValidators, passwordValidators,
            keyNormalizer, errors, services, NullLogger<UserManager<ApplicationUser>>.Instance);
    }

    private static JobController CreateController(AppDbContext db, IJobImportService jobService)
    {
        var userManager = CreateUserManager(db);
        var tempData = new TempDataDictionary(new DefaultHttpContext(), new FakeTempDataProvider());

        var controller = new JobController(jobService, userManager, NullLogger<JobController>.Instance, db)
        {
            TempData = tempData,
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
            }
        };

        return controller;
    }

    private static async Task<(Job Job, Product Product, ApplicationUser Worker)> SeedJobAsync(AppDbContext db)
    {
        var product = new Product
        {
            ProductCode = "P-100",
            ProductName = "Test Product",
            IsActive = true
        };
        db.Products.Add(product);
        await db.SaveChangesAsync();

        var template = new Template
        {
            TemplateCode = "T-100",
            Name = "Test Template",
            IsLatest = true,
            Steps =
            {
                new TemplateStep { SequenceNo = 1, Label = "Step 1", Instrument = InstrumentType.Torque, Target = 10m }
            }
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();

        var comp = new ProductComponent
        {
            ProductId = product.ProductId,
            TemplateId = template.TemplateId,
            ComponentName = "Component A",
            Quantity = 1,
            AssemblyOrder = 1
        };
        db.ProductComponents.Add(comp);

        var worker = new ApplicationUser
        {
            Id = "worker-01",
            UserName = "worker1@smarttorque.local",
            Email = "worker1@smarttorque.local",
            FullName = "Worker Joe",
            IsActive = true
        };
        db.Users.Add(worker);

        var role = new IdentityRole { Id = "role-w", Name = "Worker", NormalizedName = "WORKER" };
        db.Roles.Add(role);
        db.UserRoles.Add(new IdentityUserRole<string> { UserId = worker.Id, RoleId = role.Id });

        var job = new Job
        {
            JobNo = "JOB-2026-0001",
            ProductId = product.ProductId,
            Quantity = 5,
            QuantityCompleted = 0,
            Priority = PriorityLevel.Yellow,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job);

        await db.SaveChangesAsync();
        return (job, product, worker);
    }

    [Fact]
    public async Task Detail_ReturnsViewWithJobDetailViewModel_WhenJobExists()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Detail(job.JobId);

        var viewResult = result.Should().BeOfType<ViewResult>().Subject;
        var model = viewResult.Model.Should().BeOfType<JobDetailViewModel>().Subject;
        model.JobId.Should().Be(job.JobId);
        model.JobNo.Should().Be(job.JobNo);
        model.CanAssign.Should().BeTrue();
        model.CanHold.Should().BeTrue();
        model.CanCancel.Should().BeTrue();
        model.CanResume.Should().BeFalse(); // Not on hold yet
        model.AvailableWorkers.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Detail_ReturnsNotFound_WhenJobDoesNotExist()
    {
        using var db = CreateInMemoryDbContext();
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Detail(99999);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Assign_UpdatesWorkerAndPriority_AndRedirectsToDetail()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, worker) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Assign(job.JobId, worker.Id, (int)PriorityLevel.Red);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(JobController.Detail));
        redirect.RouteValues!["id"].Should().Be(job.JobId);

        var updated = await db.Jobs.FindAsync(job.JobId);
        updated!.AssignedWorkerId.Should().Be(worker.Id);
        updated.Priority.Should().Be(PriorityLevel.Red);
        updated.Status.Should().Be(JobStatus.Assigned);
    }

    [Fact]
    public async Task Assign_UnassignsWorker_WhenWorkerIdIsNull()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, worker) = await SeedJobAsync(db);
        job.AssignedWorkerId = worker.Id;
        job.Status = JobStatus.Assigned;
        await db.SaveChangesAsync();

        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Assign(job.JobId, null, null);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(JobController.Detail));

        var updated = await db.Jobs.FindAsync(job.JobId);
        updated!.AssignedWorkerId.Should().BeNull();
        updated.Status.Should().Be(JobStatus.Pending);
    }

    [Fact]
    public async Task Hold_PutsJobOnHold_AndRedirectsToDetail()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Hold(job.JobId, "Material shortage");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(JobController.Detail));

        var updated = await db.Jobs.FindAsync(job.JobId);
        updated!.Status.Should().Be(JobStatus.OnHold);
        controller.TempData["Success"].Should().NotBeNull();
    }

    [Fact]
    public async Task Resume_ResumesJobFromHold_AndRedirectsToDetail()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        job.Status = JobStatus.OnHold;
        await db.SaveChangesAsync();

        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Resume(job.JobId);

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(JobController.Detail));

        var updated = await db.Jobs.FindAsync(job.JobId);
        updated!.Status.Should().Be(JobStatus.Pending);
        controller.TempData["Success"].Should().NotBeNull();
    }

    [Fact]
    public async Task Cancel_CancelsJob_AndRedirectsToDetail()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Cancel(job.JobId, "Order cancelled by customer");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        redirect.ActionName.Should().Be(nameof(JobController.Detail));

        var updated = await db.Jobs.FindAsync(job.JobId);
        updated!.Status.Should().Be(JobStatus.Cancelled);
        controller.TempData["Success"].Should().NotBeNull();
    }

    [Fact]
    public async Task Hold_SetsTempDataError_WhenJobIsCompleted()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        job.Status = JobStatus.Completed;
        await db.SaveChangesAsync();

        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.Hold(job.JobId, "Hold completed");

        var redirect = result.Should().BeOfType<RedirectToActionResult>().Subject;
        controller.TempData["Error"].Should().NotBeNull();
    }

    [Fact]
    public async Task PrintUnitQrs_ReturnsAllUnits_ForJob()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.PrintUnitQrs(job.JobId);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        view.ViewName.Should().Be("PrintUnitQrs");
        var vm = view.Model.Should().BeOfType<SmartTorque.Web.ViewModels.Jobs.PrintUnitQrsViewModel>().Subject;
        vm.TotalJobs.Should().Be(1);
        vm.TotalUnits.Should().Be(job.Quantity);
        vm.Items.Should().HaveCount(job.Quantity);
    }

    [Fact]
    public async Task PrintUnitQrs_WithUnitNo_ReturnsSingleUnit()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.PrintUnitQrs(job.JobId, unitNo: 2);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<SmartTorque.Web.ViewModels.Jobs.PrintUnitQrsViewModel>().Subject;
        vm.TotalUnits.Should().Be(1);
        vm.Items.First().UnitIndex.Should().Be(2);
    }

    [Fact]
    public async Task PrintBatchQrs_WithJobIds_ReturnsUnitsOfAllSelectedJobs()
    {
        using var db = CreateInMemoryDbContext();
        var (job1, _, _) = await SeedJobAsync(db);
        var job2 = new Job
        {
            JobNo = "JOB-20261107-0002",
            ProductId = job1.ProductId,
            Quantity = 2,
            Status = JobStatus.Pending,
            Priority = PriorityLevel.Yellow,
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job2);
        await db.SaveChangesAsync();

        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.PrintBatchQrs(jobIds: $"{job1.JobId},{job2.JobId}");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<SmartTorque.Web.ViewModels.Jobs.PrintUnitQrsViewModel>().Subject;
        vm.TotalJobs.Should().Be(2);
        vm.TotalUnits.Should().Be(job1.Quantity + job2.Quantity);
    }

    [Fact]
    public async Task PrintBatchQrs_WithTokens_ReturnsSpecificUnits()
    {
        using var db = CreateInMemoryDbContext();
        var (job, _, _) = await SeedJobAsync(db);
        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        // Pre-ensure units
        await controller.PrintUnitQrs(job.JobId);
        var targetUnit = db.JobUnits.First(u => u.JobId == job.JobId && u.UnitIndex == 1);

        var result = await controller.PrintBatchQrs(tokens: targetUnit.TokenNo);

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<SmartTorque.Web.ViewModels.Jobs.PrintUnitQrsViewModel>().Subject;
        vm.TotalUnits.Should().Be(1);
        vm.Items.First().TokenNo.Should().Be(targetUnit.TokenNo);
    }

    [Fact]
    public async Task PrintBatchQrs_WithQueue_ReturnsAllUnallocatedJobs()
    {
        using var db = CreateInMemoryDbContext();
        var (job1, _, _) = await SeedJobAsync(db);
        job1.AssignedWorkerId = null;

        var job2 = new Job
        {
            JobNo = "JOB-20261107-0002",
            ProductId = job1.ProductId,
            Quantity = 2,
            Status = JobStatus.Pending,
            Priority = PriorityLevel.Yellow,
            CreatedAt = DateTime.UtcNow
        };
        db.Jobs.Add(job2);
        await db.SaveChangesAsync();

        var jobService = new JobImportService(db, new NotificationService(), new PriorityService());
        var controller = CreateController(db, jobService);

        var result = await controller.PrintBatchQrs(queue: "unallocated");

        var view = result.Should().BeOfType<ViewResult>().Subject;
        var vm = view.Model.Should().BeOfType<SmartTorque.Web.ViewModels.Jobs.PrintUnitQrsViewModel>().Subject;
        vm.TotalJobs.Should().Be(2);
        vm.TotalUnits.Should().Be(job1.Quantity + job2.Quantity);
    }
}