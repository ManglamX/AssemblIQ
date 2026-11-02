using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class GranularJobAllocationAndScopingTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Job_IsTemplateAllowed_ReturnsTrue_ForFullProduct_And_FilteredTemplates()
    {
        // 1. Full product allows all
        var fullJob = new Job
        {
            ScopeType = AllocationScopeType.FullProduct,
            AllowedTemplateIds = null
        };
        fullJob.IsTemplateAllowed(1).Should().BeTrue();
        fullJob.IsTemplateAllowed(99).Should().BeTrue();

        // 2. Selected templates allows only specified IDs
        var scopedJob = new Job
        {
            ScopeType = AllocationScopeType.SelectedTemplates,
            AllowedTemplateIds = "10,20,30"
        };
        scopedJob.IsTemplateAllowed(10).Should().BeTrue();
        scopedJob.IsTemplateAllowed(20).Should().BeTrue();
        scopedJob.IsTemplateAllowed(15).Should().BeFalse();
    }

    [Fact]
    public void Job_IsStepAllowed_ReturnsTrue_ForFullProduct_And_FilteredSteps()
    {
        // 1. Full product allows all steps
        var fullJob = new Job
        {
            ScopeType = AllocationScopeType.FullProduct,
            AllowedStepIds = null
        };
        fullJob.IsStepAllowed(1).Should().BeTrue();
        fullJob.IsStepAllowed(50).Should().BeTrue();

        // 2. Selected steps allows only designated operations
        var scopedJob = new Job
        {
            ScopeType = AllocationScopeType.SelectedSteps,
            AllowedStepIds = "101,102,105"
        };
        scopedJob.IsStepAllowed(101).Should().BeTrue();
        scopedJob.IsStepAllowed(105).Should().BeTrue();
        scopedJob.IsStepAllowed(103).Should().BeFalse();
    }

    [Fact]
    public async Task GetExecutionStateAsync_AbstractsWorkerStationView_ToOnlyAssignedTemplatesAndSteps()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var sessionService = new SessionService(db, evaluator);

        // Setup 2 templates: Frame (steps 1, 2) and Door (steps 3, 4)
        var template1 = new Template
        {
            TemplateId = 1,
            TemplateCode = "TMP-FRAME",
            Name = "Frame Template",
            Mode = TemplateMode.Torque,
            Steps =
            {
                new TemplateStep { TemplateStepId = 101, SequenceNo = 1, Label = "Bolt A", Target = 10m, ToleranceMinus = 1m, TolerancePlus = 1m },
                new TemplateStep { TemplateStepId = 102, SequenceNo = 2, Label = "Bolt B", Target = 10m, ToleranceMinus = 1m, TolerancePlus = 1m }
            }
        };

        var template2 = new Template
        {
            TemplateId = 2,
            TemplateCode = "TMP-DOOR",
            Name = "Door Template",
            Mode = TemplateMode.Torque,
            Steps =
            {
                new TemplateStep { TemplateStepId = 201, SequenceNo = 1, Label = "Hinge 1", Target = 15m, ToleranceMinus = 1m, TolerancePlus = 1m },
                new TemplateStep { TemplateStepId = 202, SequenceNo = 2, Label = "Hinge 2", Target = 15m, ToleranceMinus = 1m, TolerancePlus = 1m }
            }
        };
        db.Templates.AddRange(template1, template2);

        var product = new Product
        {
            ProductId = 1,
            ProductCode = "PRD-SECURITY-DOOR",
            ProductName = "Security Door Assembly",
            Components =
            {
                new ProductComponent { ProductComponentId = 11, ComponentName = "Main Frame", AssemblyOrder = 1, Template = template1 },
                new ProductComponent { ProductComponentId = 12, ComponentName = "Door Panel", AssemblyOrder = 2, Template = template2 }
            }
        };
        db.Products.Add(product);

        // Create Scoped Job: Assigned only Template 1 (Main Frame)
        var scopedJob = new Job
        {
            JobId = 500,
            JobNo = "JOB-SCOPED-001",
            ProductId = product.ProductId,
            Product = product,
            Quantity = 1,
            ScopeType = AllocationScopeType.SelectedTemplates,
            AllowedTemplateIds = "1",
            ScopeSummary = "Templates: Main Frame"
        };
        db.Jobs.Add(scopedJob);
        await db.SaveChangesAsync();

        var state = await sessionService.GetExecutionStateAsync(scopedJob.JobId, "worker-1");

        // Assert: Worker Station execution view is abstracted to ONLY the assigned Template 1
        state.Components.Should().HaveCount(1);
        state.Components[0].ComponentName.Should().Be("Main Frame");
        state.Components[0].TemplateId.Should().Be(1);
        state.Steps.Should().HaveCount(2);
        state.ScopeType.Should().Be(AllocationScopeType.SelectedTemplates);
        state.ScopeSummary.Should().Be("Templates: Main Frame");
    }

    [Fact]
    public async Task GetExecutionStateAsync_FiltersIndividualSteps_WhenScopeTypeIsSelectedSteps()
    {
        using var db = CreateInMemoryDbContext();
        var evaluator = new ToleranceService();
        var sessionService = new SessionService(db, evaluator);

        var template = new Template
        {
            TemplateId = 5,
            TemplateCode = "TMP-CHASSIS",
            Name = "Chassis Template",
            Mode = TemplateMode.Torque,
            Steps =
            {
                new TemplateStep { TemplateStepId = 301, SequenceNo = 1, Label = "Front Torque", Target = 20m, ToleranceMinus = 1m, TolerancePlus = 1m },
                new TemplateStep { TemplateStepId = 302, SequenceNo = 2, Label = "Mid Torque", Target = 20m, ToleranceMinus = 1m, TolerancePlus = 1m },
                new TemplateStep { TemplateStepId = 303, SequenceNo = 3, Label = "Rear Torque", Target = 20m, ToleranceMinus = 1m, TolerancePlus = 1m }
            }
        };
        db.Templates.Add(template);

        var product = new Product
        {
            ProductId = 2,
            ProductCode = "PRD-CHASSIS",
            ProductName = "Heavy Chassis",
            Components =
            {
                new ProductComponent { ProductComponentId = 21, ComponentName = "Chassis Assembly", AssemblyOrder = 1, Template = template }
            }
        };
        db.Products.Add(product);

        // Job scoped only to step 301 and step 303 (Station 1 handles front & rear, Station 2 handles mid)
        var scopedJob = new Job
        {
            JobId = 600,
            JobNo = "JOB-SCOPED-002",
            ProductId = product.ProductId,
            Product = product,
            Quantity = 1,
            ScopeType = AllocationScopeType.SelectedSteps,
            AllowedTemplateIds = "5",
            AllowedStepIds = "301,303",
            ScopeSummary = "Front and Rear Torque Operations"
        };
        db.Jobs.Add(scopedJob);
        await db.SaveChangesAsync();

        var state = await sessionService.GetExecutionStateAsync(scopedJob.JobId, "worker-station-1");

        // Assert: Worker station only sees the 2 assigned steps, skipping step 302
        state.Steps.Should().HaveCount(2);
        state.Steps.Select(s => s.StepId).Should().Contain(new[] { 301, 303 });
        state.Steps.Select(s => s.StepId).Should().NotContain(302);
    }

    [Fact]
    public async Task AssignProductToLineAsync_StoresGranularWbsScope_OnMapping()
    {
        using var db = CreateInMemoryDbContext();
        var assembly = new AssemblyService(db, null!, null!);

        var dept = new Department { DepartmentId = 1, Name = "Mechanical", Code = "MECH" };
        var line = new AssemblyLine { AssemblyLineId = 10, AssemblyName = "Main Line", AssemblyCode = "L-01", DepartmentId = 1, IsActive = true };
        var product = new Product { ProductId = 20, ProductCode = "PRD-TEST", ProductName = "Test Product", IsActive = true };
        db.Departments.Add(dept);
        db.AssemblyLines.Add(line);
        db.Products.Add(product);
        await db.SaveChangesAsync();

        // Dept Head maps product to line with SelectedTemplates scope
        await assembly.AssignProductToLineAsync(
            productId: 20,
            assemblyLineId: 10,
            isDefault: true,
            createdBy: "admin@dept.local",
            scopeType: AllocationScopeType.SelectedTemplates,
            allowedTemplateIds: "101,102",
            allowedStepIds: null,
            scopeSummary: "Templates: Chassis, Battery"
        );

        var mapping = await db.ProductAssemblyMappings.FirstOrDefaultAsync(m => m.ProductId == 20 && m.AssemblyLineId == 10);
        mapping.Should().NotBeNull();
        mapping!.ScopeType.Should().Be(AllocationScopeType.SelectedTemplates);
        mapping.AllowedTemplateIds.Should().Be("101,102");
        mapping.ScopeSummary.Should().Be("Templates: Chassis, Battery");
    }
}