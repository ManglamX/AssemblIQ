using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class ProductServiceTests
{
    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Template> AddTemplateAsync(
        AppDbContext db,
        string code,
        int version = 1,
        bool isLatest = true,
        decimal target = 25m,
        string stepLabel = "Bolt 1")
    {
        var template = new Template
        {
            TemplateCode = code,
            Version = version,
            IsLatest = isLatest,
            Name = $"{code} v{version}",
            Mode = TemplateMode.Torque,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "seed",
            Steps =
            {
                new TemplateStep
                {
                    SequenceNo = 1,
                    Instrument = InstrumentType.Torque,
                    Label = stepLabel,
                    Target = target,
                    TolerancePlus = 2m,
                    ToleranceMinus = 2m,
                    ToleranceMode = ToleranceMode.Absolute,
                    MaxRetries = 3
                }
            }
        };
        db.Templates.Add(template);
        await db.SaveChangesAsync();
        return template;
    }

    private static SaveProductRequest SampleRequest(params (int TemplateId, string Name)[] components)
        => new(
            ProductCode: "PRD-100",
            ProductName: "Test Product",
            Description: "A test product",
            ImagePath: "uploads/products/test.png",
            Components: components
                .Select(c => new ProductComponentRequest(c.TemplateId, c.Name, 1))
                .ToList());

    [Fact]
    public async Task CreateAsync_PersistsComponentsInAssemblyOrder_AndLogsAudit()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var t2 = await AddTemplateAsync(db, "PANEL-B");
        var service = new ProductService(db);

        var product = await service.CreateAsync(
            SampleRequest((t1.TemplateId, "Left Panel"), (t2.TemplateId, "Right Panel")),
            "admin@factory.com");

        product.ProductId.Should().BeGreaterThan(0);
        product.ProductCode.Should().Be("PRD-100");
        product.IsActive.Should().BeTrue();

        var saved = await db.Products.Include(p => p.Components).FirstAsync(p => p.ProductId == product.ProductId);
        saved.Components.Should().HaveCount(2);
        saved.Components.OrderBy(c => c.AssemblyOrder).Select(c => c.ComponentName)
            .Should().Equal("Left Panel", "Right Panel");

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityName == nameof(Product) && a.Action == "Created");
        audit.Should().NotBeNull();
        audit!.UserId.Should().Be("admin@factory.com");
    }

    [Fact]
    public async Task CreateAsync_UppercasesCode_AndRejectsDuplicates()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var service = new ProductService(db);

        var created = await service.CreateAsync(SampleRequest((t1.TemplateId, "Panel")), "admin");
        created.ProductCode.Should().Be("PRD-100");

        var act = () => service.CreateAsync(SampleRequest((t1.TemplateId, "Panel")), "admin");
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_RejectsProductWithoutComponents()
    {
        using var db = CreateInMemoryDbContext();
        var service = new ProductService(db);

        var act = () => service.CreateAsync(SampleRequest(), "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*at least one component*");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsComponentsOrderedWithTemplateAndSteps()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A", stepLabel: "Bolt A");
        var t2 = await AddTemplateAsync(db, "PANEL-B", stepLabel: "Bolt B");
        var service = new ProductService(db);

        var product = await service.CreateAsync(
            SampleRequest((t1.TemplateId, "First"), (t2.TemplateId, "Second")),
            "admin");

        var loaded = await service.GetByIdAsync(product.ProductId);

        loaded.Should().NotBeNull();
        loaded!.Components.Select(c => c.ComponentName).Should().Equal("First", "Second");
        loaded.Components.First().Template.Steps.Should().HaveCount(1);
        loaded.Components.First().Template.TemplateCode.Should().Be("PANEL-A");
    }

    [Fact]
    public async Task UpdateAsync_ReplacesComponents_AndKeepsNewAssemblyOrder()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var t2 = await AddTemplateAsync(db, "PANEL-B");
        var t3 = await AddTemplateAsync(db, "PANEL-C");
        var service = new ProductService(db);

        var product = await service.CreateAsync(
            SampleRequest((t1.TemplateId, "First"), (t2.TemplateId, "Second")),
            "admin");

        var updated = await service.UpdateAsync(product.ProductId, new SaveProductRequest(
            ProductCode: "PRD-100",
            ProductName: "Renamed Product",
            Description: null,
            ImagePath: null,
            Components:
            [
                new ProductComponentRequest(t3.TemplateId, "Only", 2)
            ]), "editor");

        updated.ProductName.Should().Be("Renamed Product");

        var saved = await db.Products.Include(p => p.Components).FirstAsync(p => p.ProductId == product.ProductId);
        saved.Components.Should().HaveCount(1);
        saved.Components.Single().ComponentName.Should().Be("Only");
        saved.Components.Single().Quantity.Should().Be(2);
        saved.Components.Single().AssemblyOrder.Should().Be(1);
        saved.UpdatedBy.Should().Be("editor");
    }

    [Fact]
    public async Task CloneAsync_CopiesComponents_WithUniqueCode()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var t2 = await AddTemplateAsync(db, "PANEL-B");
        var service = new ProductService(db);

        var source = await service.CreateAsync(
            SampleRequest((t1.TemplateId, "First"), (t2.TemplateId, "Second")),
            "admin");

        var clone = await service.CloneAsync(source.ProductId, "admin");
        var secondClone = await service.CloneAsync(source.ProductId, "admin");

        clone.ProductId.Should().NotBe(source.ProductId);
        clone.ProductCode.Should().Be("PRD-100-COPY");
        clone.ProductName.Should().Contain("(Copy)");
        clone.Components.Should().HaveCount(2);

        secondClone.ProductCode.Should().Be("PRD-100-COPY2");

        var audits = await db.AuditLogs.Where(a => a.Action == "Cloned").ToListAsync();
        audits.Should().HaveCount(2);
    }

    [Fact]
    public async Task SetActiveAsync_TogglesAndLogs()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var service = new ProductService(db);
        var product = await service.CreateAsync(SampleRequest((t1.TemplateId, "Panel")), "admin");

        await service.SetActiveAsync(product.ProductId, false, "admin");

        var saved = await db.Products.FirstAsync(p => p.ProductId == product.ProductId);
        saved.IsActive.Should().BeFalse();

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "Deactivated");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task IsCodeUniqueAsync_IsCaseInsensitive_AndSupportsExclusion()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var service = new ProductService(db);
        var product = await service.CreateAsync(SampleRequest((t1.TemplateId, "Panel")), "admin");

        (await service.IsCodeUniqueAsync("prd-100")).Should().BeFalse();
        (await service.IsCodeUniqueAsync("prd-100", product.ProductId)).Should().BeTrue();
        (await service.IsCodeUniqueAsync("prd-200")).Should().BeTrue();
    }

    [Fact]
    public async Task PreviewUpdateToLatestAsync_DetectsNewerVersionAndChangedSteps()
    {
        using var db = CreateInMemoryDbContext();
        var v1 = await AddTemplateAsync(db, "PANEL-X", version: 1, isLatest: false, target: 25m);
        var v2 = await AddTemplateAsync(db, "PANEL-X", version: 2, isLatest: true, target: 30m);
        var service = new ProductService(db);

        var product = await service.CreateAsync(SampleRequest((v1.TemplateId, "Panel")), "admin");
        var componentId = product.Components.Single().ProductComponentId;

        var preview = await service.PreviewUpdateToLatestAsync(componentId);

        preview.IsAlreadyLatest.Should().BeFalse();
        preview.OldVersion.Should().Be(1);
        preview.NewVersion.Should().Be(2);
        preview.OldSteps.Single().Target.Should().Be(25m);
        preview.NewSteps.Single().Target.Should().Be(30m);
        preview.ChangedLabels.Should().Contain("Bolt 1");
    }

    [Fact]
    public async Task ApplyUpdateToLatestAsync_RepointsComponentToLatestVersion()
    {
        using var db = CreateInMemoryDbContext();
        var v1 = await AddTemplateAsync(db, "PANEL-Y", version: 1, isLatest: false);
        var v2 = await AddTemplateAsync(db, "PANEL-Y", version: 2, isLatest: true, target: 40m);
        var service = new ProductService(db);

        var product = await service.CreateAsync(SampleRequest((v1.TemplateId, "Panel")), "admin");
        var componentId = product.Components.Single().ProductComponentId;

        await service.ApplyUpdateToLatestAsync(componentId, "admin");

        var saved = await db.ProductComponents.FirstAsync(c => c.ProductComponentId == componentId);
        saved.TemplateId.Should().Be(v2.TemplateId);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.Action == "ComponentVersionUpdated");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task ApplyUpdateToLatestAsync_WhenAlreadyLatest_Throws()
    {
        using var db = CreateInMemoryDbContext();
        var v1 = await AddTemplateAsync(db, "PANEL-Z");
        var service = new ProductService(db);

        var product = await service.CreateAsync(SampleRequest((v1.TemplateId, "Panel")), "admin");
        var componentId = product.Components.Single().ProductComponentId;

        var act = () => service.ApplyUpdateToLatestAsync(componentId, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already on the latest*");
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchAndActive()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A");
        var t2 = await AddTemplateAsync(db, "PANEL-B");
        var service = new ProductService(db);

        var p1 = await service.CreateAsync(new SaveProductRequest(
            "AAA-1", "Alpha Locker", null, null,
            [new ProductComponentRequest(t1.TemplateId, "A", 1)]), "admin");
        await service.CreateAsync(new SaveProductRequest(
            "BBB-2", "Beta Cabinet", null, null,
            [new ProductComponentRequest(t2.TemplateId, "B", 1)]), "admin");
        await service.SetActiveAsync(p1.ProductId, false, "admin");

        var bySearch = await service.ListAsync(new ProductFilter(Search: "Alpha"));
        bySearch.Should().HaveCount(1);
        bySearch[0].ProductCode.Should().Be("AAA-1");

        var activeOnly = await service.ListAsync(new ProductFilter(IsActive: true));
        activeOnly.Should().HaveCount(1);
        activeOnly[0].ProductCode.Should().Be("BBB-2");
    }

    [Fact]
    public async Task GetByCodeAsync_ReturnsProductWithComponentsAndSteps()
    {
        using var db = CreateInMemoryDbContext();
        var t1 = await AddTemplateAsync(db, "PANEL-A", target: 30m);
        var service = new ProductService(db);

        await service.CreateAsync(new SaveProductRequest(
            "ITEM-12345", "Test Shelf", "Sample desc", null,
            [new ProductComponentRequest(t1.TemplateId, "Main Panel", 1)]), "admin");

        var found = await service.GetByCodeAsync("ITEM-12345");
        found.Should().NotBeNull();
        found!.ProductName.Should().Be("Test Shelf");
        found.Components.Should().HaveCount(1);
        found.Components.Single().Template.Steps.Should().HaveCount(1);
        found.Components.Single().Template.Steps.Single().Target.Should().Be(30m);

        var notFound = await service.GetByCodeAsync("NON-EXISTENT");
        notFound.Should().BeNull();
    }
}