using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class TemplateServiceTests
{
    private class FakeImageUploadService : IImageUploadService
    {
        public Task<string> SaveAsync(Stream imageStream, string originalFileName, long fileSizeBytes, long? maxBytes = null, CancellationToken ct = default)
            => Task.FromResult("uploads/templates/test.png");

        public Task<string> SaveAsync(Stream imageStream, string originalFileName, long fileSizeBytes, string subfolder, long? maxBytes = null, CancellationToken ct = default)
            => Task.FromResult($"uploads/{subfolder}/test.png");

        public void Delete(string? relativePath) { }
    }

    private class TestableTemplateService : TemplateService
    {
        public bool SimulateUsedBySession { get; set; }

        public TestableTemplateService(AppDbContext db, IImageUploadService images)
            : base(db, images) { }

        public override Task<bool> IsUsedBySessionAsync(int templateId, CancellationToken ct = default)
            => Task.FromResult(SimulateUsedBySession);
    }

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static CreateTemplateRequest SampleRequest(string code = "TPL-001", string name = "Sample Panel") =>
        new(
            TemplateCode: code,
            Name: name,
            Description: "Sample description",
            Mode: TemplateMode.Torque,
            ImagePath: "uploads/templates/sample.png",
            Steps:
            [
                new StepRequest(
                    SequenceNo: 1,
                    Instrument: InstrumentType.Torque,
                    Label: "Bolt A",
                    Target: 25.0m,
                    TolerancePlus: 2.0m,
                    ToleranceMinus: 2.0m,
                    ToleranceMode: ToleranceMode.Absolute,
                    MaxRetries: 3,
                    MarkerX: 10.5m,
                    MarkerY: 20.5m
                ),
                new StepRequest(
                    SequenceNo: 2,
                    Instrument: InstrumentType.Torque,
                    Label: "Bolt B",
                    Target: 30.0m,
                    TolerancePlus: 1.5m,
                    ToleranceMinus: 1.5m,
                    ToleranceMode: ToleranceMode.Absolute,
                    MaxRetries: 3,
                    MarkerX: 30.0m,
                    MarkerY: 40.0m
                )
            ]
        );

    [Fact]
    public async Task CreateAsync_CreatesTemplateWithVersion1_AndLogsAudit()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TemplateService(db, new FakeImageUploadService());

        var result = await service.CreateAsync(SampleRequest("PANEL-A", "Locker Panel"), "admin@factory.com");

        result.Should().NotBeNull();
        result.TemplateCode.Should().Be("PANEL-A");
        result.Version.Should().Be(1);
        result.IsLatest.Should().BeTrue();
        result.IsActive.Should().BeTrue();
        result.Steps.Should().HaveCount(2);

        var saved = await db.Templates.Include(t => t.Steps).FirstOrDefaultAsync(t => t.TemplateId == result.TemplateId);
        saved.Should().NotBeNull();
        saved!.Steps.Should().HaveCount(2);

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == "PANEL-A");
        audit.Should().NotBeNull();
        audit!.Action.Should().Be("Created");
        audit.UserId.Should().Be("admin@factory.com");
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExists_CreatesVersion2_AndUnmarksPreviousLatest()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TemplateService(db, new FakeImageUploadService());

        var v1 = await service.CreateAsync(SampleRequest("CODE-DUP", "Original"), "admin@factory.com");
        v1.Version.Should().Be(1);
        v1.IsLatest.Should().BeTrue();

        var v2 = await service.CreateAsync(SampleRequest("CODE-DUP", "Revision 2"), "admin@factory.com");
        v2.Version.Should().Be(2);
        v2.IsLatest.Should().BeTrue();

        var reloadedV1 = await db.Templates.FindAsync(v1.TemplateId);
        reloadedV1!.IsLatest.Should().BeFalse();
    }

    [Fact]
    public async Task EditAsync_WhenUnused_EditsInPlace_KeepsVersionAndId()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TestableTemplateService(db, new FakeImageUploadService())
        {
            SimulateUsedBySession = false
        };

        var created = await service.CreateAsync(SampleRequest("PANEL-B", "Original Name"), "admin@factory.com");
        var originalId = created.TemplateId;

        var updatedRequest = new CreateTemplateRequest(
            TemplateCode: "PANEL-B",
            Name: "Updated Name",
            Description: "Updated desc",
            Mode: TemplateMode.Torque,
            ImagePath: "uploads/templates/new.png",
            Steps:
            [
                new StepRequest(
                    SequenceNo: 1,
                    Instrument: InstrumentType.Torque,
                    Label: "Single Bolt",
                    Target: 50.0m,
                    TolerancePlus: 5.0m,
                    ToleranceMinus: 5.0m,
                    ToleranceMode: ToleranceMode.Percent,
                    MaxRetries: 2,
                    MarkerX: 50.0m,
                    MarkerY: 50.0m
                )
            ]
        );

        var edited = await service.EditAsync(originalId, updatedRequest, "editor@factory.com");

        edited.TemplateId.Should().Be(originalId);
        edited.Version.Should().Be(1);
        edited.Name.Should().Be("Updated Name");
        edited.IsLatest.Should().BeTrue();
        edited.Steps.Should().HaveCount(1);
        edited.Steps.First().Label.Should().Be("Single Bolt");

        var allTemplates = await db.Templates.Where(t => t.TemplateCode == "PANEL-B").ToListAsync();
        allTemplates.Should().HaveCount(1);
    }

    [Fact]
    public async Task EditAsync_WhenUsedBySession_CreatesNewVersion_AndUnmarksOldVersion()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TestableTemplateService(db, new FakeImageUploadService())
        {
            SimulateUsedBySession = true
        };

        var created = await service.CreateAsync(SampleRequest("PANEL-C", "Original Version 1"), "admin@factory.com");
        var v1Id = created.TemplateId;

        var updateRequest = new CreateTemplateRequest(
            TemplateCode: "PANEL-C",
            Name: "New Version 2 Name",
            Description: "Modified steps",
            Mode: TemplateMode.Torque,
            ImagePath: "uploads/templates/v2.png",
            Steps:
            [
                new StepRequest(
                    SequenceNo: 1,
                    Instrument: InstrumentType.Torque,
                    Label: "New Step 1",
                    Target: 40.0m,
                    TolerancePlus: 4.0m,
                    ToleranceMinus: 4.0m,
                    ToleranceMode: ToleranceMode.Absolute,
                    MaxRetries: 3,
                    MarkerX: null,
                    MarkerY: null
                )
            ]
        );

        var v2 = await service.EditAsync(v1Id, updateRequest, "editor@factory.com");

        v2.TemplateId.Should().NotBe(v1Id);
        v2.Version.Should().Be(2);
        v2.IsLatest.Should().BeTrue();
        v2.Name.Should().Be("New Version 2 Name");
        v2.Steps.Should().HaveCount(1);

        var v1 = await db.Templates.FindAsync(v1Id);
        v1.Should().NotBeNull();
        v1!.IsLatest.Should().BeFalse();
        v1.Version.Should().Be(1);

        var audits = await db.AuditLogs.Where(a => a.EntityId == "PANEL-C").ToListAsync();
        audits.Should().Contain(a => a.Action == "VersionCreated");
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyLatestVersions_AndAppliesFilters()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TestableTemplateService(db, new FakeImageUploadService())
        {
            SimulateUsedBySession = true
        };

        // Create Panel 1 and update it so it has v1 and v2
        var p1v1 = await service.CreateAsync(SampleRequest("BOX-01", "Storage Box"), "admin@factory.com");
        await service.EditAsync(p1v1.TemplateId, SampleRequest("BOX-01", "Storage Box v2"), "admin@factory.com");

        // Create Panel 2 (Torque mode)
        await service.CreateAsync(SampleRequest("BOX-02", "Battery Box"), "admin@factory.com");

        // Create Panel 3 (Vernier mode)
        var vernierRequest = new CreateTemplateRequest(
            TemplateCode: "BOX-03",
            Name: "Bracket Vernier",
            Description: null,
            Mode: TemplateMode.Vernier,
            ImagePath: null,
            Steps:
            [
                new StepRequest(1, InstrumentType.Vernier, "Length Check", 100m, 0.5m, 0.5m, ToleranceMode.Absolute, 3, null, null)
            ]
        );
        await service.CreateAsync(vernierRequest, "admin@factory.com");

        // 1. List all latest: should be 3 items (BOX-01 v2, BOX-02 v1, BOX-03 v1), not v1 of BOX-01
        var allLatest = await service.ListAsync(new TemplateFilter());
        allLatest.Should().HaveCount(3);
        allLatest.Should().OnlyContain(t => t.IsLatest);

        // 2. Search filter
        var searchResult = await service.ListAsync(new TemplateFilter(Search: "Bracket"));
        searchResult.Should().HaveCount(1);
        searchResult[0].TemplateCode.Should().Be("BOX-03");

        // 3. Mode filter
        var vernierOnly = await service.ListAsync(new TemplateFilter(Mode: TemplateMode.Vernier));
        vernierOnly.Should().HaveCount(1);
        vernierOnly[0].TemplateCode.Should().Be("BOX-03");
    }

    [Fact]
    public async Task SetActiveAsync_TogglesIsActive_AndLogsAudit()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TemplateService(db, new FakeImageUploadService());

        var created = await service.CreateAsync(SampleRequest("TPL-ACT", "Toggle Test"), "admin@factory.com");
        created.IsActive.Should().BeTrue();

        await service.SetActiveAsync(created.TemplateId, false, "admin@factory.com");

        var deact = await db.Templates.FindAsync(created.TemplateId);
        deact!.IsActive.Should().BeFalse();

        var audit = await db.AuditLogs.FirstOrDefaultAsync(a => a.EntityId == created.TemplateId.ToString() && a.Action == "Deactivated");
        audit.Should().NotBeNull();
    }

    [Fact]
    public async Task IsCodeUniqueAsync_CorrectlyValidatesUniqueness()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TemplateService(db, new FakeImageUploadService());

        await service.CreateAsync(SampleRequest("CODE-100", "Panel"), "admin@factory.com");

        var isUniqueNew = await service.IsCodeUniqueAsync("CODE-200");
        isUniqueNew.Should().BeTrue();

        var isUniqueExisting = await service.IsCodeUniqueAsync("code-100");
        isUniqueExisting.Should().BeFalse();

        var existing = await service.GetLatestByCodeAsync("CODE-100");
        var isUniqueSelf = await service.IsCodeUniqueAsync("CODE-100", excludeTemplateId: existing!.TemplateId);
        isUniqueSelf.Should().BeTrue();
    }

    [Fact]
    public async Task GetAllVersionsByCodeAsync_ReturnsAllVersionsInDescendingOrder()
    {
        using var db = CreateInMemoryDbContext();
        var service = new TestableTemplateService(db, new FakeImageUploadService())
        {
            SimulateUsedBySession = true
        };

        var v1 = await service.CreateAsync(SampleRequest("MULTI-VER", "Version 1"), "admin@factory.com");
        var v2 = await service.EditAsync(v1.TemplateId, SampleRequest("MULTI-VER", "Version 2"), "admin@factory.com");
        var v3 = await service.EditAsync(v2.TemplateId, SampleRequest("MULTI-VER", "Version 3"), "admin@factory.com");

        var versions = await service.GetAllVersionsByCodeAsync("MULTI-VER");

        versions.Should().HaveCount(3);
        versions[0].Version.Should().Be(3);
        versions[0].IsLatest.Should().BeTrue();
        versions[1].Version.Should().Be(2);
        versions[1].IsLatest.Should().BeFalse();
        versions[2].Version.Should().Be(1);
        versions[2].IsLatest.Should().BeFalse();
    }
}