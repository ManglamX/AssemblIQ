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
/// Fixture-based tests for the BM parser (PRD BM-2, Architecture §7.3).
/// The .xlsx files live in Fixtures/ and are copied to the test output directory.
/// </summary>
public class BmImportServiceTests
{
    private const string DefaultHeaders = "bm-default-headers.xlsx";
    private const string CustomHeaders = "bm-custom-headers.xlsx";

    private static AppDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task SeedProductsAsync(AppDbContext db, params (string Code, bool Active)[] products)
    {
        foreach (var (code, active) in products)
        {
            db.Products.Add(new Product
            {
                ProductCode = code,
                ProductName = code,
                IsActive = active,
                CreatedAt = DateTime.UtcNow,
                CreatedBy = "test"
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>The five shades from Architecture §5.3, as the seeder creates them.</summary>
    private static async Task SeedColourMapAsync(AppDbContext db)
    {
        var palette = new (string Hex, PriorityLevel Priority, string Label)[]
        {
            ("#000000", PriorityLevel.Black, "Black"),
            ("#FF0000", PriorityLevel.Red, "Red"),
            ("#FFFF00", PriorityLevel.Yellow, "Yellow"),
            ("#00B050", PriorityLevel.Green, "Green"),
            ("#FFFFFF", PriorityLevel.White, "White")
        };

        foreach (var (hex, priority, label) in palette)
        {
            db.PriorityColourMap.Add(new PriorityColourMap
            {
                ColourHex = hex,
                Priority = priority,
                Label = label,
                MatchTolerance = 40,
                IsActive = true
            });
        }

        await db.SaveChangesAsync();
    }

    private static async Task<BmImportService> CreateServiceAsync(AppDbContext db, string? headerMapJson = null)
    {
        if (headerMapJson != null)
        {
            db.Settings.Add(new Setting { Key = BmSettings.HeaderMapKey, Value = headerMapJson });
            await db.SaveChangesAsync();
        }

        return new BmImportService(db);
    }

    private static async Task<BmParseResult> ParseAsync(BmImportService service, string fixture)
    {
        await using var stream = File.OpenRead(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", fixture));

        return await service.ParseAsync(stream, fixture);
    }

    // =======================================================================
    // Header map configuration
    // =======================================================================

    [Fact]
    public async Task GetHeaderMapAsync_ReturnsDefault_WhenSettingIsMissing()
    {
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db);

        var map = await service.GetHeaderMapAsync();

        map.ProductCode.Should().Be("ProductCode");
        map.Quantity.Should().Be("Quantity");
        map.DueDate.Should().Be("DueDate");
        map.Remarks.Should().Be("Remarks");
    }

    [Fact]
    public async Task GetHeaderMapAsync_ReadsConfiguredJson()
    {
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var map = await service.GetHeaderMapAsync();

        map.ProductCode.Should().Be("Product Code");
        map.Quantity.Should().Be("Qty");
        map.DueDate.Should().Be("Due Date");
    }

    [Fact]
    public async Task GetHeaderMapAsync_FallsBackToDefault_WhenJsonIsCorrupt()
    {
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db, "{ not valid json");

        var map = await service.GetHeaderMapAsync();

        map.ProductCode.Should().Be("ProductCode");
    }

    // =======================================================================
    // Row reading (default headers)
    // =======================================================================

    [Fact]
    public async Task ParseAsync_DefaultHeaders_ReadsEveryMappedField()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        await SeedColourMapAsync(db);
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.HasFatalError.Should().BeFalse();
        result.SheetName.Should().Be("BM");
        result.Rows.Should().HaveCount(4);
        result.OkCount.Should().Be(4);

        var first = result.Rows.Single(r => r.ExcelRowNo == 2);
        first.ProductCode.Should().Be("LK-6P");
        first.Quantity.Should().Be(5);
        first.DueDate.Should().Be(new DateOnly(2026, 10, 20));
        first.Remarks.Should().Be("Routine stock");
        first.Status.Should().Be(BmRowImportStatus.Ok);
    }

    [Fact]
    public async Task ParseAsync_DefaultHeaders_AllowsBlankRemarks()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.Rows.Single(r => r.ExcelRowNo == 3).Remarks.Should().BeNull();
    }

    [Fact]
    public async Task ParseAsync_SkipsRowsThatCarryNothing()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        // Row 4 of the fixture is deliberately empty.
        result.Rows.Select(r => r.ExcelRowNo).Should().Equal(2, 3, 5, 6);
    }

    [Fact]
    public async Task ParseAsync_ParsesTextDate()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.Rows.Single(r => r.ExcelRowNo == 5)
            .DueDate.Should().Be(new DateOnly(2026, 11, 5));
    }

    [Fact]
    public async Task ParseAsync_ParsesExcelSerialDate()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.Rows.Single(r => r.ExcelRowNo == 6)
            .DueDate.Should().Be(DateOnly.FromDateTime(DateTime.FromOADate(45990)));
    }

    // =======================================================================
    // Header mapping is configurable
    // =======================================================================

    [Fact]
    public async Task ParseAsync_AppliesConfiguredHeaderMap()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        result.HasFatalError.Should().BeFalse();
        result.HeaderMap.Quantity.Should().Be("Qty");
        result.Rows.Select(r => r.ExcelRowNo).Should().Equal(2, 3, 4, 5, 6, 7);
    }

    [Fact]
    public async Task ParseAsync_FailsCleanly_WhenHeadingsDoNotMatchTheMap()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db); // default map

        var result = await ParseAsync(service, CustomHeaders);

        result.HasFatalError.Should().BeTrue();
        result.Rows.Should().BeEmpty();
        result.FileErrors.Should().ContainSingle()
            .Which.Should().Contain("product code column");
    }

    // =======================================================================
    // File-level failures
    // =======================================================================

    [Fact]
    public async Task ParseAsync_FailsCleanly_OnANonWorkbookFile()
    {
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db);

        BmParseResult result;
        await using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("this is not a spreadsheet")))
        {
            result = await service.ParseAsync(stream, "notes.txt");
        }

        result.HasFatalError.Should().BeTrue();
        result.FileErrors.Should().ContainSingle()
            .Which.Should().Contain("not a valid .xlsx");
    }

    [Fact]
    public async Task ParseAsync_FailsCleanly_OnAnEmptyWorksheet()
    {
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, "bm-empty.xlsx");

        result.HasFatalError.Should().BeTrue();
        result.FileErrors.Should().ContainSingle()
            .Which.Should().Contain("empty");
    }

    // =======================================================================
    // Product-code lookup
    // =======================================================================

    [Fact]
    public async Task ParseAsync_FlagsUnknownProduct()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        var row = result.Rows.Single(r => r.ExcelRowNo == 3);
        row.Status.Should().Be(BmRowImportStatus.Error);
        row.Message.Should().Contain("'NOSUCH-1' was not found");
    }

    [Fact]
    public async Task ParseAsync_FlagsInactiveProduct()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", false));
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.Rows.Should().OnlyContain(r =>
            r.Status == BmRowImportStatus.Error &&
            r.Message!.Contains("is not active"));
    }

    // =======================================================================
    // Quantity and date parsing
    // =======================================================================

    [Fact]
    public async Task ParseAsync_FlagsQuantityZero()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        var row = result.Rows.Single(r => r.ExcelRowNo == 4);
        row.Status.Should().Be(BmRowImportStatus.Error);
        row.Quantity.Should().BeNull();
        row.Message.Should().Contain("Quantity must be a whole number greater than 0.");
    }

    [Fact]
    public async Task ParseAsync_FlagsNonNumericQuantity()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        var row = result.Rows.Single(r => r.ExcelRowNo == 5);
        row.Status.Should().Be(BmRowImportStatus.Error);
        row.Message.Should().Contain("Quantity must be a whole number greater than 0.");
    }

    [Fact]
    public async Task ParseAsync_FlagsUnparseableDate()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        var row = result.Rows.Single(r => r.ExcelRowNo == 6);
        row.Status.Should().Be(BmRowImportStatus.Error);
        row.DueDate.Should().BeNull();
        row.Message.Should().Contain("Due date is not a valid date.");
    }

    // =======================================================================
    // Colour reading (basic; the detector itself lands in P4.4)
    // =======================================================================

    [Fact]
    public async Task ParseAsync_ReportsSolidFillColour_AndWhiteForUnfilledCells()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        // Row 7 of the fixture has a solid black product-code cell.
        result.Rows.Single(r => r.ExcelRowNo == 7).ColourHex.Should().Be("#000000");

        // BM-4: a cell with no fill is White.
        result.Rows.Where(r => r.ExcelRowNo != 7).Should()
            .OnlyContain(r => r.ColourHex == "#FFFFFF");
    }

    [Fact]
    public async Task ParseAsync_TreatsUnfilledCellsAsWhite_AndMatchesTheWhiteShade()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        await SeedColourMapAsync(db);
        var service = await CreateServiceAsync(db);

        var result = await ParseAsync(service, DefaultHeaders);

        result.Rows.Should().OnlyContain(r => r.ColourHex == "#FFFFFF");
        result.Rows.Should().OnlyContain(r => r.Priority == PriorityLevel.White);
    }

    [Fact]
    public async Task ParseAsync_AssignsPriorityFromTheDetectedColour()
    {
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        await SeedColourMapAsync(db);
        var service = await CreateServiceAsync(db, CustomHeaderMapJson);

        var result = await ParseAsync(service, CustomHeaders);

        // Row 7 is filled black, so it must come out as the Black priority.
        result.Rows.Single(r => r.ExcelRowNo == 7).Priority.Should().Be(PriorityLevel.Black);
        result.Rows.Single(r => r.ExcelRowNo == 7).Status.Should().Be(BmRowImportStatus.Ok);

        // Rows with no fill are White.
        result.Rows.Where(r => r.ExcelRowNo != 7).Should()
            .OnlyContain(r => r.Priority == PriorityLevel.White);
    }

    [Fact]
    public async Task InspectHeadersAsync_DiscoversColumnsAndIdentifiesItemCodeAndDescription()
    {
        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("Sheet1");
            string[] headers = ["Sales Order", "Project", "Pos", "PONo", "Item Code", "Finished Good Item Desc", "Qty.Reqd", "Qty.Pend", "RM Release", "Due Date", "Custom"];
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(1, i + 1).Value = headers[i];
            }
            wb.SaveAs(stream);
        }

        stream.Position = 0;
        using var db = CreateInMemoryDbContext();
        var service = await CreateServiceAsync(db);

        var inspection = await service.InspectHeadersAsync(stream);

        inspection.AllHeaders.Should().HaveCount(11);
        inspection.DetectedItemCodeColumn.Should().Be("Item Code");
        inspection.SuggestedDescriptionColumn.Should().Be("Finished Good Item Desc");
        inspection.DetectedQuantityColumn.Should().Be("Qty.Reqd");
        inspection.DetectedDueDateColumn.Should().Be("Due Date");
        inspection.DetectedSalesOrderColumn.Should().Be("Sales Order");
        inspection.DetectedPONoColumn.Should().Be("PONo");
    }

    [Fact]
    public async Task ParseAsync_FindsItemCodeAndDescription_AndDetectsRowColours()
    {
        using var stream = new MemoryStream();
        using (var wb = new ClosedXML.Excel.XLWorkbook())
        {
            var ws = wb.Worksheets.Add("BM");
            string[] headers = ["Sales Order", "PONo", "Item Code", "Finished Good Item Desc", "Qty.Reqd", "Due Date"];
            for (var i = 0; i < headers.Length; i++)
                ws.Cell(1, i + 1).Value = headers[i];

            // Row 2: Black background
            ws.Cell(2, 1).Value = "SO-1001";
            ws.Cell(2, 2).Value = "PO-2001";
            ws.Cell(2, 3).Value = "LK-6P";
            ws.Cell(2, 4).Value = "Locker 6 Panel Grey";
            ws.Cell(2, 5).Value = 6;
            ws.Cell(2, 6).Value = new DateTime(2026, 12, 12);
            ws.Row(2).Style.Fill.PatternType = ClosedXML.Excel.XLFillPatternValues.Solid;
            ws.Row(2).Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.Black;

            wb.SaveAs(stream);
        }

        stream.Position = 0;
        using var db = CreateInMemoryDbContext();
        await SeedProductsAsync(db, ("LK-6P", true));
        await SeedColourMapAsync(db);
        var service = await CreateServiceAsync(db);

        var result = await service.ParseAsync(stream, "test.xlsx");

        result.HasFatalError.Should().BeFalse();
        result.Rows.Should().HaveCount(1);
        var row = result.Rows[0];
        row.ProductCode.Should().Be("LK-6P");
        row.Remarks.Should().Be("Locker 6 Panel Grey");
        row.SalesOrder.Should().Be("SO-1001");
        row.PONo.Should().Be("PO-2001");
        row.Quantity.Should().Be(6);
        row.DueDate.Should().Be(new DateOnly(2026, 12, 12));
        row.ColourHex.Should().Be("#000000");
        row.Priority.Should().Be(PriorityLevel.Black);
    }

    // =======================================================================

    /// <summary>Headings matching the friendly names the parser tests exercise.</summary>
    private const string CustomHeaderMapJson =
        "{\"ProductCode\":\"Product Code\",\"Quantity\":\"Qty\",\"DueDate\":\"Due Date\",\"Remarks\":\"Remarks\"}";
}