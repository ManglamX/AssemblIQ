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
}
