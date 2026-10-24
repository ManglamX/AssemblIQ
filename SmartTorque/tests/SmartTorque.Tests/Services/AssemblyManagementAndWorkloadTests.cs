using FluentAssertions;

using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class AssemblyManagementAndWorkloadTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly AssemblyService _assemblyService;
    private readonly NotificationService _notificationService;
    private readonly PriorityService _priorityService;

    public AssemblyManagementAndWorkloadTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _notificationService = new NotificationService();
        _priorityService = new PriorityService();
        _assemblyService = new AssemblyService(_db, _notificationService, _priorityService);

        // Seed Worker role
        _db.Roles.Add(new IdentityRole { Id = "role-worker", Name = "Worker", NormalizedName = "WORKER" });
        _db.SaveChanges();
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    private async Task<ApplicationUser> CreateWorkerAsync(string id, string name, int? lineId = null, bool isActive = true)
    {
        var user = new ApplicationUser
        {
            Id = id,
            UserName = id,
            FullName = name,
            IsActive = isActive,
            CurrentAssemblyLineId = lineId
        };
        _db.Users.Add(user);
        _db.UserRoles.Add(new IdentityUserRole<string> { RoleId = "role-worker", UserId = id });
        await _db.SaveChangesAsync();
        return user;
    }

    private async Task<Product> CreateProductAsync(string code, string name, bool isActive = true)
    {
        var product = new Product
        {
            ProductCode = code,
            ProductName = name,
            IsActive = isActive,
            CreatedAt = DateTime.UtcNow
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync();
        return product;
    }

    [Fact]
    public async Task AssemblyLine_CRUD_And_Validation_WorksCorrectly()
    {
        // 1. Create
        var line = await _assemblyService.CreateAssemblyLineAsync(
            new CreateAssemblyLineRequest("LINE-1", "Chassis Assembly", "Mechanical", "Station 1-5", true),
            "admin");

        line.AssemblyLineId.Should().BeGreaterThan(0);
        line.AssemblyCode.Should().Be("LINE-1");

        // 2. Reject duplicate code
        var duplicateAct = () => _assemblyService.CreateAssemblyLineAsync(
            new CreateAssemblyLineRequest("LINE-1", "Duplicate Line", "Mechanical", null, true),
            "admin");
        await duplicateAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");

        // 3. Update
        var updated = await _assemblyService.UpdateAssemblyLineAsync(
            line.AssemblyLineId,
            new UpdateAssemblyLineRequest("LINE-1-MOD", "Chassis Line Updated", "Fabrication", "Notes", true),
            "admin");
        updated.AssemblyCode.Should().Be("LINE-1-MOD");
        updated.Department.Should().Be("Fabrication");

        // 4. Toggle Status
        var activeAfterToggle = await _assemblyService.ToggleAssemblyLineStatusAsync(line.AssemblyLineId, "admin");
        activeAfterToggle.Should().BeFalse();

        var recheck = await _assemblyService.GetAssemblyLineDetailAsync(line.AssemblyLineId);
        recheck!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ProductToAssemblyMapping_DynamicAssignment_And_Defaults_WorkCorrectly()
    {
        var p1 = await CreateProductAsync("LKR-6P", "6-Panel Locker");
        var p2 = await CreateProductAsync("DSK-01", "Executive Desk");
        var line1 = await _assemblyService.CreateAssemblyLineAsync(
            new CreateAssemblyLineRequest("LINE-A", "Line A", "Assembly"), "admin");
        var line2 = await _assemblyService.CreateAssemblyLineAsync(
            new CreateAssemblyLineRequest("LINE-B", "Line B", "Assembly"), "admin");

        // Initially both products are unmapped
        var unmapped = await _assemblyService.GetUnmappedProductsAsync();
        unmapped.Should().HaveCount(2);

        // Assign p1 to line1 (default)
        await _assemblyService.AssignProductToLineAsync(p1.ProductId, line1.AssemblyLineId, isDefault: true, "admin");

        // Assign p1 also to line2 (multi-line option), but not default
        await _assemblyService.AssignProductToLineAsync(p1.ProductId, line2.AssemblyLineId, isDefault: false, "admin");

        var mappings = await _assemblyService.GetLineProductMappingsAsync(line1.AssemblyLineId);
        mappings.Should().HaveCount(1);
        mappings[0].ProductCode.Should().Be("LKR-6P");
        mappings[0].IsDefault.Should().BeTrue();

        // Switch default line for p1 to line2
        await _assemblyService.SetDefaultLineForProductAsync(p1.ProductId, line2.AssemblyLineId, "admin");

        var line2Mappings = await _assemblyService.GetLineProductMappingsAsync(line2.AssemblyLineId);
        line2Mappings.First(m => m.ProductId == p1.ProductId).IsDefault.Should().BeTrue();

        var line1MappingsUpdated = await _assemblyService.GetLineProductMappingsAsync(line1.AssemblyLineId);
        line1MappingsUpdated.First(m => m.ProductId == p1.ProductId).IsDefault.Should().BeFalse();

        // Unmapped products now only contains p2
        var unmappedAfter = await _assemblyService.GetUnmappedProductsAsync();
        unmappedAfter.Should().ContainSingle(p => p.ProductCode == "DSK-01");
    }

    [Fact]
    public async Task Mapping_InactiveProductOrLine_IsProhibited()
    {
        var activeLine = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("ACT-LINE", "Active", "Dept"), "admin");
        var inactiveLine = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("INACT-LINE", "Inactive", "Dept", null, false), "admin");

        var activeProduct = await CreateProductAsync("PROD-ACT", "Active Prod", true);
        var inactiveProduct = await CreateProductAsync("PROD-INACT", "Inactive Prod", false);

        // Attempt mapping inactive product
        var act1 = () => _assemblyService.AssignProductToLineAsync(inactiveProduct.ProductId, activeLine.AssemblyLineId, true, "admin");
        await act1.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive product*");

        // Attempt mapping to inactive line
        var act2 = () => _assemblyService.AssignProductToLineAsync(activeProduct.ProductId, inactiveLine.AssemblyLineId, true, "admin");
        await act2.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive assembly line*");
    }

    [Fact]
    public async Task WorkerAssemblyAssignment_Persistence_And_Validation_WorksCorrectly()
    {
        var lineA = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("STN-A", "Station A", "Dept"), "admin");
        var lineB = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("STN-B", "Station B", "Dept"), "admin");
        var inactiveLine = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("STN-INACT", "Station Off", "Dept", null, false), "admin");

        var worker = await CreateWorkerAsync("w-john", "John Doe");

        // Assign to Station A
        await _assemblyService.SetWorkerAssemblyLineAsync(worker.Id, lineA.AssemblyLineId, "admin");
        var currentLine = await _assemblyService.GetWorkerCurrentLineAsync(worker.Id);
        currentLine.Should().NotBeNull();
        currentLine!.AssemblyCode.Should().Be("STN-A");

        // Switch to Station B
        await _assemblyService.SetWorkerAssemblyLineAsync(worker.Id, lineB.AssemblyLineId, "admin");
        var switchedLine = await _assemblyService.GetWorkerCurrentLineAsync(worker.Id);
        switchedLine!.AssemblyCode.Should().Be("STN-B");

        // Reject assigning to inactive line
        var rejectAct = () => _assemblyService.SetWorkerAssemblyLineAsync(worker.Id, inactiveLine.AssemblyLineId, "admin");
        await rejectAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*inactive assembly line*");
    }

    [Fact]
    public async Task WorkloadBasedAutoAssign_AllocatesToLeastLoadedWorker_OnDesignatedLine()
    {
        // Setup Line and Product
        var line = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("ASM-W1", "Line 1", "Dept"), "admin");
        var product = await CreateProductAsync("PRD-1", "Product 1");
        await _assemblyService.AssignProductToLineAsync(product.ProductId, line.AssemblyLineId, true, "admin");

        // Setup 2 Workers on this line
        var w1 = await CreateWorkerAsync("worker-1", "Alice", line.AssemblyLineId);
        var w2 = await CreateWorkerAsync("worker-2", "Bob", line.AssemblyLineId);

        // Pre-existing load: Worker 1 has active task of 3.0 pts, Worker 2 has 1.0 pt
        _db.Jobs.AddRange(
            new Job
            {
                JobNo = "JOB-EXIST-1",
                ProductId = product.ProductId,
                AssemblyLineId = line.AssemblyLineId,
                AssignedWorkerId = w1.Id,
                Status = JobStatus.InProgress,
                EstimatedWorkload = 3.0m
            },
            new Job
            {
                JobNo = "JOB-EXIST-2",
                ProductId = product.ProductId,
                AssemblyLineId = line.AssemblyLineId,
                AssignedWorkerId = w2.Id,
                Status = JobStatus.Assigned,
                EstimatedWorkload = 1.0m
            }
        );

        // Create 2 open pending jobs on this line (each with workload 1.0)
        var pending1 = new Job
        {
            JobNo = "JOB-PEND-1",
            ProductId = product.ProductId,
            AssemblyLineId = line.AssemblyLineId,
            Status = JobStatus.Pending,
            EstimatedWorkload = 1.0m,
            Priority = PriorityLevel.Red
        };
        var pending2 = new Job
        {
            JobNo = "JOB-PEND-2",
            ProductId = product.ProductId,
            AssemblyLineId = line.AssemblyLineId,
            Status = JobStatus.Pending,
            EstimatedWorkload = 1.0m,
            Priority = PriorityLevel.Green
        };
        _db.Jobs.AddRange(pending1, pending2);
        await _db.SaveChangesAsync();

        // Run auto-assign
        var result = await _assemblyService.AutoAssignJobsAsync("admin");
        result.TotalAssigned.Should().Be(2);

        // First pending job (Red priority) should go to Worker 2 (workload 1.0 < 3.0).
        // After assignment, Worker 2 workload becomes 2.0.
        // Second pending job should STILL go to Worker 2 (workload 2.0 < 3.0).
        await _db.Entry(pending1).ReloadAsync();
        await _db.Entry(pending2).ReloadAsync();

        pending1.AssignedWorkerId.Should().Be(w2.Id);
        pending2.AssignedWorkerId.Should().Be(w2.Id);
        pending1.Status.Should().Be(JobStatus.Assigned);
        pending1.AssignmentMode.Should().Be(AssignmentMode.Auto);
    }

    [Fact]
    public async Task StrictAssemblyBoundary_WorkerOnLineB_NeverGetsTasksForLineA()
    {
        var lineA = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("LINE-AAA", "Line A", "Dept"), "admin");
        var lineB = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("LINE-BBB", "Line B", "Dept"), "admin");

        var prodA = await CreateProductAsync("PROD-AAA", "Prod A");
        await _assemblyService.AssignProductToLineAsync(prodA.ProductId, lineA.AssemblyLineId, true, "admin");

        // Worker on Line A has high workload (5.0 pts)
        var workerA = await CreateWorkerAsync("w-lineA", "Worker A", lineA.AssemblyLineId);
        _db.Jobs.Add(new Job
        {
            JobNo = "JOB-HEAVY",
            ProductId = prodA.ProductId,
            AssemblyLineId = lineA.AssemblyLineId,
            AssignedWorkerId = workerA.Id,
            Status = JobStatus.InProgress,
            EstimatedWorkload = 5.0m
        });

        // Worker on Line B is completely free (0.0 pts workload)
        var workerB = await CreateWorkerAsync("w-lineB", "Worker B", lineB.AssemblyLineId);

        // New pending task arrives for Line A
        var newTaskLineA = new Job
        {
            JobNo = "JOB-TASK-A",
            ProductId = prodA.ProductId,
            AssemblyLineId = lineA.AssemblyLineId,
            Status = JobStatus.Pending,
            EstimatedWorkload = 1.0m
        };
        _db.Jobs.Add(newTaskLineA);
        await _db.SaveChangesAsync();

        // Run auto-assign
        var result = await _assemblyService.AutoAssignJobsAsync("admin");
        result.TotalAssigned.Should().Be(1);

        await _db.Entry(newTaskLineA).ReloadAsync();

        // Strict boundary: Must assign to workerA (Line A), NEVER workerB (Line B)
        newTaskLineA.AssignedWorkerId.Should().Be(workerA.Id);
        newTaskLineA.AssignedWorkerId.Should().NotBe(workerB.Id);
    }

    [Fact]
    public async Task UnassignedFallback_WhenNoActiveWorkersOnLine_TasksRemainPending_And_AlertGenerated()
    {
        var line = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("SOLO-LINE", "Solo Line", "Dept"), "admin");
        var prod = await CreateProductAsync("SOLO-PROD", "Solo Prod");
        await _assemblyService.AssignProductToLineAsync(prod.ProductId, line.AssemblyLineId, true, "admin");

        // Line has NO active workers assigned!
        var job = new Job
        {
            JobNo = "JOB-UNSTAFFED",
            ProductId = prod.ProductId,
            AssemblyLineId = line.AssemblyLineId,
            Status = JobStatus.Pending,
            EstimatedWorkload = 2.0m
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var result = await _assemblyService.AutoAssignJobsAsync("admin");
        result.TotalAssigned.Should().Be(0);
        result.TotalUnassigned.Should().Be(1);

        await _db.Entry(job).ReloadAsync();
        job.AssignedWorkerId.Should().BeNull();
        job.Status.Should().Be(JobStatus.Pending);

        // An alert notification must be raised for Admin
        var notifications = await _db.Notifications.Where(n => n.Type == NotificationType.AssemblyAlert).ToListAsync();
        notifications.Should().ContainSingle();
        notifications[0].TargetRole.Should().Be("Admin");
        notifications[0].Message.Should().Contain("SOLO-LINE");
    }

    [Fact]
    public async Task WorkloadCalculation_ExcludesCompletedCancelledAndOnHoldTasks()
    {
        var line = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("CALC-LINE", "Calc Line", "Dept"), "admin");
        var worker = await CreateWorkerAsync("calc-worker", "Calc Worker", line.AssemblyLineId);
        var prod = await CreateProductAsync("CALC-PROD", "Calc Prod");

        _db.Jobs.AddRange(
            // Should be counted: Assigned (1.0) + InProgress (2.5) = 3.5
            new Job { JobNo = "J1", ProductId = prod.ProductId, AssignedWorkerId = worker.Id, Status = JobStatus.Assigned, EstimatedWorkload = 1.0m },
            new Job { JobNo = "J2", ProductId = prod.ProductId, AssignedWorkerId = worker.Id, Status = JobStatus.InProgress, EstimatedWorkload = 2.5m },

            // Must NOT be counted:
            new Job { JobNo = "J3", ProductId = prod.ProductId, AssignedWorkerId = worker.Id, Status = JobStatus.Completed, EstimatedWorkload = 4.0m },
            new Job { JobNo = "J4", ProductId = prod.ProductId, AssignedWorkerId = worker.Id, Status = JobStatus.Cancelled, EstimatedWorkload = 3.0m },
            new Job { JobNo = "J5", ProductId = prod.ProductId, AssignedWorkerId = worker.Id, Status = JobStatus.OnHold, EstimatedWorkload = 5.0m }
        );
        await _db.SaveChangesAsync();

        var workload = await _assemblyService.GetWorkerActiveWorkloadAsync(worker.Id);
        workload.Should().Be(3.5m);
    }

    [Fact]
    public async Task ManualReassignJob_EnforcesAssemblyLineBoundary()
    {
        var lineA = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("REASSIGN-A", "Line A", "Dept"), "admin");
        var lineB = await _assemblyService.CreateAssemblyLineAsync(new CreateAssemblyLineRequest("REASSIGN-B", "Line B", "Dept"), "admin");

        var workerA = await CreateWorkerAsync("w-rea-A", "Worker A", lineA.AssemblyLineId);
        var workerB = await CreateWorkerAsync("w-rea-B", "Worker B", lineB.AssemblyLineId);

        var prod = await CreateProductAsync("PROD-RE", "Prod RE");
        var jobOnLineA = new Job
        {
            JobNo = "JOB-LINE-A-01",
            ProductId = prod.ProductId,
            AssemblyLineId = lineA.AssemblyLineId,
            Status = JobStatus.Pending,
            EstimatedWorkload = 1.0m
        };
        _db.Jobs.Add(jobOnLineA);
        await _db.SaveChangesAsync();

        // Attempting to assign workerB (stationed on Line B) to a task designated for Line A
        var invalidAct = () => _assemblyService.ManualReassignJobAsync(jobOnLineA.JobId, workerB.Id, null, null, "admin");
        await invalidAct.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*can only be assigned to workers operating on that line*");

        // Assigning workerA (stationed on Line A) succeeds
        var reassigned = await _assemblyService.ManualReassignJobAsync(jobOnLineA.JobId, workerA.Id, 2.5m, null, "admin");
        reassigned.AssignedWorkerId.Should().Be(workerA.Id);
        reassigned.Status.Should().Be(JobStatus.Assigned);
        reassigned.EstimatedWorkload.Should().Be(2.5m);
    }
}