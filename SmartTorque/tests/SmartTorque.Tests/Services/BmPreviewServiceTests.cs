using System.Text;

using FluentAssertions;

using Microsoft.EntityFrameworkCore;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Data;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>
/// Tests for holding a BM workbook as a preview (PRD BM-7, BM-8). Uses the real
/// <see cref="BmFileStore"/> against a temp wwwroot and the real parser against the
/// committed fixtures.
/// </summary>
public class BmPreviewServiceTests : IDisposable
{
    private const string DefaultHeaders = "bm-default-headers.xlsx";
    private const string CustomHeaders = "bm-custom-headers.xlsx";

    private const string CustomHeaderMapJson =
        "{\"ProductCode\":\"Product Code\",\"Quantity\":\"Qty\",\"DueDate\":\"Due Date\",\"Remarks\":\"Remarks\"}";

    private readonly AppDbContext _db;
    private readonly string _webRoot;
    private readonly BmFileStore _files;
    private readonly BmImportService _import;
    private readonly BmPreviewService _service;

    public BmPreviewServiceTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        _db = new AppDbContext(options);
        _webRoot = Path.Combine(Path.GetTempPath(), "bm-preview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_webRoot);

        _files = new BmFileStore(_webRoot);
        _import = new BmImportService(_db);
        _service = new BmPreviewService(_db, _import, _files);
    }

    public void Dispose()
    {
        _db.Dispose();
        try { Directory.Delete(_webRoot, true); } catch (IOException) { }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static FileStream OpenFixture(string name)
        => File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", name));

    private async Task<BmUpload> UploadAsync(string fixture, string fileName = "bm.xlsx")
    {
        await using var stream = OpenFixture(fixture);
        return await _service.UploadAsync(stream, fileName, stream.Length, "admin");
    }

    private async Task SeedProductsAsync(params (string Code, bool Active)[] products)
    {
        foreach (var (code, active) in products)
        {
            _db.Products.Add(new Product
            {
                ProductCode = code,
                ProductName = code,
                IsActive = active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });
        }

        await _db.SaveChangesAsync();
    }

    private async Task SeedColourMapAsync(params (string Hex, PriorityLevel Priority)[] shades)
    {
        foreach (var (hex, priority) in shades)
        {
            _db.PriorityColourMap.Add(new PriorityColourMap
            {
                ColourHex = hex,
                Priority = priority,
                Label = priority.ToString(),
                MatchTolerance = 40,
                IsActive = true
            });
        }

        await _db.SaveChangesAsync();
    }

    private Task UseCustomHeaderMapAsync()
    {
        _db.Settings.Add(new Setting { Key = BmSettings.HeaderMapKey, Value = CustomHeaderMapJson });
        return _db.SaveChangesAsync();
    }

    // =======================================================================
    // Uploading
    // =======================================================================

    [Fact]
    public async Task UploadAsync_PersistsTheUploadAndItsRows()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black), ("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);

        upload.UploadId.Should().BeGreaterThan(0);
        upload.Status.Should().Be(BmUploadStatus.Previewed);
        upload.RowCount.Should().Be(4);
        upload.ErrorCount.Should().Be(0);
        upload.UploadedBy.Should().Be("admin");
        upload.StoredPath.Should().StartWith("uploads/bm/");

        var saved = await _db.BmUploads.Include(u => u.Rows).SingleAsync(u => u.UploadId == upload.UploadId);
        saved.Rows.Should().HaveCount(4);
        saved.Rows.Single(r => r.ExcelRowNo == 2).ProductCode.Should().Be("LK-6P");
        saved.Rows.Single(r => r.ExcelRowNo == 2).Quantity.Should().Be(5);
    }

    [Fact]
    public async Task UploadAsync_WritesTheWorkbookToDisk()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);

        var fullPath = Path.Combine(_webRoot, upload.StoredPath.Replace('/', Path.DirectorySeparatorChar));
        File.Exists(fullPath).Should().BeTrue();
        new FileInfo(fullPath).Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task UploadAsync_MarksAnUnreadableWorkbookAsFailed()
    {
        var bytes = Encoding.UTF8.GetBytes("this is not a spreadsheet");
        await using var stream = new MemoryStream(bytes);

        var upload = await _service.UploadAsync(stream, "notes.xlsx", bytes.Length, "admin");

        upload.Status.Should().Be(BmUploadStatus.Failed);
        upload.RowCount.Should().Be(0);
        upload.ErrorCount.Should().Be(1);
        (await _db.BmRows.CountAsync(r => r.UploadId == upload.UploadId)).Should().Be(0);
    }

    [Fact]
    public async Task UploadAsync_RejectsANonXlsxFile_WithoutStoringAnything()
    {
        var countBefore = await _db.BmUploads.CountAsync();

        var bytes = Encoding.UTF8.GetBytes("plain text");
        await using var stream = new MemoryStream(bytes);

        var act = () => _service.UploadAsync(stream, "notes.txt", bytes.Length, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*.xlsx*");

        (await _db.BmUploads.CountAsync()).Should().Be(countBefore);

        // The file is rejected before anything is written to disk.
        var bmFolder = Path.Combine(_webRoot, "uploads", "bm");
        (Directory.Exists(bmFolder) ? Directory.GetFiles(bmFolder) : []).Should().BeEmpty();
    }

    [Fact]
    public async Task UploadAsync_RejectsAnOversizedFile()
    {
        await using var stream = new MemoryStream(new byte[16]);

        var act = () => _service.UploadAsync(stream, "big.xlsx", 11 * 1024 * 1024, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*10 MB*");
    }

    [Fact]
    public async Task UploadAsync_RejectsAnEmptyFile()
    {
        await using var stream = new MemoryStream();

        var act = () => _service.UploadAsync(stream, "empty.xlsx", 0, "admin");

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    // =======================================================================
    // Reading back
    // =======================================================================

    [Fact]
    public async Task GetAsync_ReturnsTheRowsInExcelOrder()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var loaded = await _service.GetAsync(upload.UploadId);

        loaded.Should().NotBeNull();
        loaded!.Rows.Select(r => r.ExcelRowNo).Should().ContainInOrder(2, 3, 5, 6);
    }

    [Fact]
    public async Task GetAsync_ReturnsNullForAnUnknownUpload()
    {
        (await _service.GetAsync(9999)).Should().BeNull();
    }

    [Fact]
    public async Task RecentAsync_ReturnsNewestFirst()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var first = await UploadAsync(DefaultHeaders);
        await Task.Delay(5);
        var second = await UploadAsync(DefaultHeaders);

        var recent = await _service.RecentAsync();

        recent.Select(u => u.UploadId).Should().ContainInOrder(second.UploadId, first.UploadId);
    }

    // =======================================================================
    // Priority override
    // =======================================================================

    [Fact]
    public async Task SetRowPriorityAsync_UpdatesTheRow()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.Single(r => r.ExcelRowNo == 2);

        await _service.SetRowPriorityAsync(row.RowId, PriorityLevel.Black);

        (await _db.BmRows.SingleAsync(r => r.RowId == row.RowId)).Priority.Should().Be(PriorityLevel.Black);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task SetRowPriorityAsync_RejectsAnOutOfRangePriority(int priority)
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.First();

        var act = () => _service.SetRowPriorityAsync(row.RowId, (PriorityLevel)priority);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task SetRowPriorityAsync_ThrowsForAnUnknownRow()
    {
        var act = () => _service.SetRowPriorityAsync(9999, PriorityLevel.Black);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // =======================================================================
    // Skip / restore
    // =======================================================================

    [Fact]
    public async Task SetRowSkippedAsync_SkipsAndRestoresAnOkRow()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.Single(r => r.ExcelRowNo == 2);

        await _service.SetRowSkippedAsync(row.RowId, true);
        (await _db.BmRows.SingleAsync(r => r.RowId == row.RowId)).ImportStatus
            .Should().Be(BmRowImportStatus.Skipped);

        await _service.SetRowSkippedAsync(row.RowId, false);
        (await _db.BmRows.SingleAsync(r => r.RowId == row.RowId)).ImportStatus
            .Should().Be(BmRowImportStatus.Ok);
    }

    [Fact]
    public async Task SetRowSkippedAsync_RestoresTheWarning_WhenTheColourIsUnrecognised()
    {
        // Only Black is mapped, so the unfilled (White) rows warn.
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black));
        await UseCustomHeaderMapAsync();

        var upload = await UploadAsync(CustomHeaders);
        var warningRow = upload.Rows.Single(r => r.ExcelRowNo == 2);
        warningRow.ImportStatus.Should().Be(BmRowImportStatus.Warning);

        await _service.SetRowSkippedAsync(warningRow.RowId, true);
        await _service.SetRowSkippedAsync(warningRow.RowId, false);

        (await _db.BmRows.SingleAsync(r => r.RowId == warningRow.RowId)).ImportStatus
            .Should().Be(BmRowImportStatus.Warning);
    }

    [Fact]
    public async Task SetRowSkippedAsync_RestoresAnOkRow_WhenTheColourMatches()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#000000", PriorityLevel.Black), ("#FFFFFF", PriorityLevel.White));
        await UseCustomHeaderMapAsync();

        var upload = await UploadAsync(CustomHeaders);
        var blackRow = upload.Rows.Single(r => r.ExcelRowNo == 7); // filled black
        blackRow.ImportStatus.Should().Be(BmRowImportStatus.Ok);

        await _service.SetRowSkippedAsync(blackRow.RowId, true);
        await _service.SetRowSkippedAsync(blackRow.RowId, false);

        (await _db.BmRows.SingleAsync(r => r.RowId == blackRow.RowId)).ImportStatus
            .Should().Be(BmRowImportStatus.Ok);
    }

    [Fact]
    public async Task SetRowSkippedAsync_RefusesToSkipAnErrorRow()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        await UseCustomHeaderMapAsync();

        var upload = await UploadAsync(CustomHeaders);
        var errorRow = upload.Rows.Single(r => r.ExcelRowNo == 4); // quantity zero

        var act = () => _service.SetRowSkippedAsync(errorRow.RowId, true);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*can be skipped*");
    }

    // =======================================================================
    // Editing an imported upload
    // =======================================================================

    [Fact]
    public async Task EditingIsRefusedOnceTheUploadHasBeenImported()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.First();

        upload.Status = BmUploadStatus.Confirmed;
        await _db.SaveChangesAsync();

        var priority = () => _service.SetRowPriorityAsync(row.RowId, PriorityLevel.Black);
        var skip = () => _service.SetRowSkippedAsync(row.RowId, true);

        await priority.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been imported*");
        await skip.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already been imported*");
    }

    // =======================================================================
    // Cancelling
    // =======================================================================

    [Fact]
    public async Task CancelAsync_RemovesTheUploadItsRowsAndTheFile()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        var fullPath = Path.Combine(_webRoot, upload.StoredPath.Replace('/', Path.DirectorySeparatorChar));

        await _service.CancelAsync(upload.UploadId);

        (await _db.BmUploads.AnyAsync(u => u.UploadId == upload.UploadId)).Should().BeFalse();
        (await _db.BmRows.AnyAsync(r => r.UploadId == upload.UploadId)).Should().BeFalse();
        File.Exists(fullPath).Should().BeFalse();
    }

    [Fact]
    public async Task CancelAsync_RefusesToRemoveAnImportedUpload()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);
        upload.Status = BmUploadStatus.Confirmed;
        await _db.SaveChangesAsync();

        var act = () => _service.CancelAsync(upload.UploadId);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*cannot be cancelled*");
    }

    // =======================================================================
    // Recovering file-level errors
    // =======================================================================

    [Fact]
    public async Task ReadFileErrorsAsync_ReturnsNothingForAPreviewedUpload()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        var upload = await UploadAsync(DefaultHeaders);

        (await _service.ReadFileErrorsAsync(upload)).Should().BeEmpty();
    }

    [Fact]
    public async Task ReadFileErrorsAsync_RecoversTheReasonForAFailedUpload()
    {
        var bytes = Encoding.UTF8.GetBytes("this is not a spreadsheet");
        await using var stream = new MemoryStream(bytes);

        var upload = await _service.UploadAsync(stream, "notes.xlsx", bytes.Length, "admin");

        var errors = await _service.ReadFileErrorsAsync(upload);

        errors.Should().ContainSingle()
            .Which.Should().Contain("not a valid .xlsx");
    }

    [Fact]
    public async Task ReadFileErrorsAsync_ReportsAMissingFile()
    {
        var bytes = Encoding.UTF8.GetBytes("this is not a spreadsheet");
        await using var stream = new MemoryStream(bytes);

        var upload = await _service.UploadAsync(stream, "notes.xlsx", bytes.Length, "admin");
        _files.Delete(upload.StoredPath);

        var errors = await _service.ReadFileErrorsAsync(upload);

        errors.Should().ContainSingle()
            .Which.Should().Contain("no longer available");
    }

    // =======================================================================
    // Re-evaluation without re-uploading
    // =======================================================================

    [Fact]
    public async Task ReevaluateRowsAsync_TransitionsErrorToOk_WhenProductIsCreated()
    {
        // Upload without seeding product LK-6P
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.First(r => r.ProductCode == "LK-6P");
        row.ImportStatus.Should().Be(BmRowImportStatus.Error);
        row.ImportMessage.Should().Contain("was not found");
        upload.ErrorCount.Should().BeGreaterThan(0);

        // Now create the product in the system
        await SeedProductsAsync(("LK-6P", true));

        // Re-evaluate (e.g. by revisiting preview page or calling GetAsync)
        await _service.ReevaluateRowsAsync(upload);

        row.ImportStatus.Should().Be(BmRowImportStatus.Ok);
        row.ImportMessage.Should().BeNull();
    }

    [Fact]
    public async Task ReevaluateRowsAsync_MarksRowImported_WhenMatchingJobAlreadyExists()
    {
        await SeedProductsAsync(("LK-6P", true));
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));
        var upload = await UploadAsync(DefaultHeaders);
        var row = upload.Rows.First(r => r.ProductCode == "LK-6P");
        row.ImportStatus.Should().Be(BmRowImportStatus.Ok);

        var product = _db.Products.First(p => p.ProductCode == "LK-6P");

        // Add a job representing that this row was already ordered/imported
        _db.Jobs.Add(new Job
        {
            JobNo = "JOB-TEST-001",
            ProductId = product.ProductId,
            SourceBmRowId = row.RowId,
            Status = JobStatus.Pending,
            Priority = PriorityLevel.White
        });
        await _db.SaveChangesAsync();

        await _service.ReevaluateRowsAsync(upload);

        row.ImportStatus.Should().Be(BmRowImportStatus.Imported);
    }

    [Fact]
    public async Task ReevaluateRowsAsync_WhenProductBelongsToAnotherDepartment_MarksErrorAndPreventsImportedStatus()
    {
        var dept1 = new Department { Name = "Engg Cell", Code = "ENGG", IsActive = true };
        var dept2 = new Department { Name = "SDLC", Code = "SDLC", IsActive = true };
        _db.Departments.AddRange(dept1, dept2);

        var sdlcUser = new ApplicationUser
        {
            UserName = "sdlc.admin@godrej.com",
            Email = "sdlc.admin@godrej.com",
            FullName = "SDLC Admin",
            Department = dept2,
            IsActive = true
        };
        _db.Users.Add(sdlcUser);
        await _db.SaveChangesAsync();

        // Seed product LK-6P under Engg Cell
        var enggProduct = new Product
        {
            ProductCode = "LK-6P",
            ProductName = "Engg Product",
            DepartmentId = dept1.DepartmentId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = "test"
        };
        _db.Products.Add(enggProduct);
        await SeedColourMapAsync(("#FFFFFF", PriorityLevel.White));

        // SDLC uploads the BM
        await using var stream = OpenFixture(DefaultHeaders);
        var upload = await _service.UploadAsync(stream, "bm.xlsx", stream.Length, sdlcUser.UserName);

        // Also add a job in Engg Cell for this product
        _db.Jobs.Add(new Job
        {
            JobNo = "JOB-ENGG-001",
            ProductId = enggProduct.ProductId,
            SourceBmRowId = upload.Rows.First(r => r.ProductCode == "LK-6P").RowId,
            Status = JobStatus.Pending,
            Priority = PriorityLevel.White
        });
        await _db.SaveChangesAsync();

        await _service.ReevaluateRowsAsync(upload);

        var row = upload.Rows.First(r => r.ProductCode == "LK-6P");
        row.ImportStatus.Should().Be(BmRowImportStatus.Error);
        row.ImportMessage.Should().Contain("belongs to Engg Cell");
        row.ImportMessage.Should().Contain("cannot be imported by SDLC");
    }
}