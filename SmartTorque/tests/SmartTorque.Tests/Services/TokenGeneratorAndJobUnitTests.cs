using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;
using Xunit;

namespace SmartTorque.Tests.Services;

public class TokenGeneratorAndJobUnitTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly JobImportService _jobImportService;

    public TokenGeneratorAndJobUnitTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _jobImportService = new JobImportService(_db, new NotificationService(), new PriorityService());
    }

    public void Dispose()
    {
        _db.Dispose();
    }

    [Fact]
    public void GenerateProductUnitToken_MustReturnStrictly40Characters()
    {
        // Act
        var token = TokenGenerator.GenerateProductUnitToken();

        // Assert
        token.Should().NotBeNullOrWhiteSpace();
        token.Length.Should().Be(40);
        token.Should().StartWith("PRD-");
    }

    [Fact]
    public void GenerateProductUnitToken_MultipleGenerations_MustBeUnique()
    {
        // Act
        var tokens = Enumerable.Range(0, 500)
            .Select(_ => TokenGenerator.GenerateProductUnitToken())
            .ToList();

        // Assert
        tokens.Should().OnlyHaveUniqueItems();
        tokens.All(t => t.Length == 40).Should().BeTrue();
    }

    [Fact]
    public async Task ConfirmAsync_CreatesUnitsForEachQuantity_With40CharTokens()
    {
        // Arrange
        var product = new Product
        {
            ProductCode = "MTR-UNIT-01",
            ProductName = "Motor Assembly Unit",
            IsActive = true
        };
        _db.Products.Add(product);

        var upload = new BmUpload
        {
            FileName = "unit-test-bm.xlsx",
            StoredPath = "fake/path.xlsx",
            Status = BmUploadStatus.Previewed,
            UploadedBy = "admin@smarttorque.local"
        };
        _db.BmUploads.Add(upload);
        await _db.SaveChangesAsync();

        var row = new BmRow
        {
            UploadId = upload.UploadId,
            ExcelRowNo = 2,
            ProductCode = "MTR-UNIT-01",
            Quantity = 5,
            DueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)),
            Priority = PriorityLevel.Green,
            SalesOrder = "SO-2026-UNIT",
            PONo = "PO-2026-UNIT",
            ImportStatus = BmRowImportStatus.Ok
        };
        _db.BmRows.Add(row);
        await _db.SaveChangesAsync();

        // Act
        var result = await _jobImportService.ConfirmAsync(upload.UploadId, "admin@smarttorque.local");

        // Assert
        result.JobsCreated.Should().Be(1);
        result.JobNumbers.Should().HaveCount(1);
        var jobNo = result.JobNumbers.First();

        var jobWithUnits = await _jobImportService.GetJobByNoAsync(jobNo);
        jobWithUnits.Should().NotBeNull();
        jobWithUnits!.Units.Should().HaveCount(5);

        // Verify each unit has 1-based index and strictly 40-character token
        for (var i = 0; i < 5; i++)
        {
            var unit = jobWithUnits.Units[i];
            unit.UnitIndex.Should().Be(i + 1);
            unit.TokenNo.Should().NotBeNullOrWhiteSpace();
            unit.TokenNo.Length.Should().Be(40);
            unit.TokenNo.Should().StartWith("PRD-");
            unit.Status.Should().Be(UnitAssemblyStatus.Pending);
        }

        // Verify tokens within the job are all distinct
        jobWithUnits.Units.Select(u => u.TokenNo).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SessionService_StartWithSpecificUnit_AndSwitchUnit_UpdatesStateAndTokens()
    {
        // Arrange
        var template = new Template
        {
            TemplateCode = "T-UNIT-SELECT",
            Name = "Unit Selection Template",
            Mode = TemplateMode.Torque,
            IsLatest = true,
            Steps =
            {
                new TemplateStep
                {
                    SequenceNo = 1,
                    Label = "Bolt 1",
                    Instrument = InstrumentType.Torque,
                    Target = 20.0m,
                    TolerancePlus = 1.0m,
                    ToleranceMinus = 1.0m,
                    ToleranceMode = ToleranceMode.Absolute,
                    MaxRetries = 999
                }
            }
        };

        var product = new Product
        {
            ProductCode = "PRD-SELECT-01",
            ProductName = "Selectable Product",
            IsActive = true,
            Components =
            {
                new ProductComponent
                {
                    Template = template,
                    ComponentName = "Main Plate",
                    AssemblyOrder = 1,
                    Quantity = 1
                }
            }
        };
        _db.Products.Add(product);

        var job = new Job
        {
            JobNo = "JOB-SELECT-001",
            Product = product,
            Quantity = 3,
            QuantityCompleted = 0,
            Priority = PriorityLevel.Green,
            Status = JobStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };
        _db.Jobs.Add(job);
        await _db.SaveChangesAsync();

        var evaluator = new ToleranceService();
        var sessionService = new SessionService(_db, evaluator);

        // Act 1: Worker selects Unit #2
        var session = await sessionService.StartOrResumeSessionAsync(job.JobId, "worker-1", unitNo: 2);

        // Assert 1
        session.UnitNo.Should().Be(2);

        var state = await sessionService.GetExecutionStateAsync(job.JobId, "worker-1");
        state.UnitNo.Should().Be(2);
        state.Units.Should().HaveCount(3);
        state.Units!.First(u => u.UnitIndex == 2).IsCurrent.Should().BeTrue();
        state.UnitTokenNo.Should().NotBeNull();
        state.UnitTokenNo!.Length.Should().Be(40);

        // Act 2: Worker switches to Unit #3
        var switchedSession = await sessionService.SwitchUnitAsync(job.JobId, 3, "worker-1");

        // Assert 2
        switchedSession.UnitNo.Should().Be(3);
        var state2 = await sessionService.GetExecutionStateAsync(job.JobId, "worker-1");
        state2.UnitNo.Should().Be(3);
        state2.UnitTokenNo.Should().Be(state2.Units!.First(u => u.UnitIndex == 3).TokenNo);
    }
}
