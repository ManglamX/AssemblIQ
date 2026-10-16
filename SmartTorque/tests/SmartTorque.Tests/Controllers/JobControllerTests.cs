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

        var controller = new JobController(jobService, userManager, NullLogger<JobController>.Instance)
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
}