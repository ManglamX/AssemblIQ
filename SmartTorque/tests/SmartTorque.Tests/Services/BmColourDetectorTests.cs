using ClosedXML.Excel;

using FluentAssertions;

using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>
/// Tests for resolving a BM cell fill to a colour (PRD BM-4, Architecture §7.3 step 4).
/// The workbook cases save and reload in memory so they exercise ClosedXML's real reader.
/// </summary>
public class BmColourDetectorTests
{
    private static string? Detect(Action<IXLWorksheet> arrange)
    {
        using var stream = new MemoryStream();

        using (var workbook = new XLWorkbook())
        {
            var sheet = workbook.Worksheets.Add("T");
            arrange(sheet);
            workbook.SaveAs(stream);
        }

        stream.Position = 0;

        using var read = new XLWorkbook(stream);
        return BmColourDetector.Detect(read, read.Worksheet(1).Cell(1, 1));
    }

    private static void SetSolid(IXLCell cell, XLColor colour)
    {
        cell.Style.Fill.PatternType = XLFillPatternValues.Solid;
        cell.Style.Fill.BackgroundColor = colour;
    }

    // -----------------------------------------------------------------------
    // Tint maths (ECMA-376)
    // -----------------------------------------------------------------------

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(255)]
    public void ApplyTint_IsIdentityWhenTintIsZero(byte channel)
    {
        BmColourDetector.ApplyTint(channel, 0d).Should().Be(channel);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(255)]
    public void ApplyTint_IsUnchangedAtTheBranchBoundary(byte channel)
    {
        // ECMA-376 uses 0.5 as the boundary between the lightening and darkening
        // branches, so it lands back on the original channel rather than on white.
        BmColourDetector.ApplyTint(channel, 0.5d).Should().Be(channel);
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(0.25)]
    [InlineData(0.45)]
    public void ApplyTint_LightensBelowHalfTint(double tint)
    {
        BmColourDetector.ApplyTint(50, tint).Should().BeGreaterThan(50);
    }

    [Theory]
    [InlineData(0.55)]
    [InlineData(0.75)]
    [InlineData(0.95)]
    public void ApplyTint_DarkensAboveHalfTint(double tint)
    {
        BmColourDetector.ApplyTint(200, tint).Should().BeLessThan(200);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(64)]
    [InlineData(128)]
    [InlineData(255)]
    public void ApplyTint_DrivesToBlackAtFullTint(byte channel)
    {
        BmColourDetector.ApplyTint(channel, 1d).Should().Be(0);
    }

    [Fact]
    public void ApplyTint_IsClampedOutsideTheDocumentedRange()
    {
        BmColourDetector.ApplyTint(100, -1d).Should().Be(100);
        BmColourDetector.ApplyTint(100, 2d).Should().Be(0);
    }

    [Fact]
    public void ApplyTint_LightensMonotonicallyBelowHalf()
    {
        byte previous = 0;
        for (var t = 0d; t < 0.5d; t += 0.05d)
        {
            var current = BmColourDetector.ApplyTint(50, t);
            current.Should().BeGreaterThanOrEqualTo(previous);
            previous = current;
        }
    }

    // -----------------------------------------------------------------------
    // ToHex
    // -----------------------------------------------------------------------

    [Fact]
    public void ToHex_RendersSixUppercaseDigits()
    {
        BmColourDetector.ToHex(18, 52, 86).Should().Be("#123456");
        BmColourDetector.ToHex(0, 0, 0).Should().Be("#000000");
        BmColourDetector.ToHex(255, 255, 255).Should().Be("#FFFFFF");
    }

    // -----------------------------------------------------------------------
    // Workbook reading
    // -----------------------------------------------------------------------

    [Fact]
    public void Detect_ReturnsNull_WhenThereIsNoCell()
    {
        using var stream = new MemoryStream();
        using (var workbook = new XLWorkbook())
        {
            workbook.Worksheets.Add("T");
            workbook.SaveAs(stream);
        }

        stream.Position = 0;
        using var read = new XLWorkbook(stream);

        BmColourDetector.Detect(read, null).Should().BeNull();
    }

    [Fact]
    public void Detect_TreatsAnUnfilledCellAsWhite()
    {
        // PRD BM-4: no fill means the row is the lowest priority.
        Detect(sheet => { }).Should().Be("#FFFFFF");
    }

    [Fact]
    public void Detect_TreatsAPatternFillAsWhite()
    {
        var hex = Detect(sheet =>
        {
            sheet.Cell(1, 1).Style.Fill.PatternType = XLFillPatternValues.Gray125;
        });

        hex.Should().Be("#FFFFFF");
    }

    [Fact]
    public void Detect_ReadsASolidRgbFill()
    {
        var hex = Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.FromArgb(18, 52, 86)));

        hex.Should().Be("#123456");
    }

    [Fact]
    public void Detect_ReadsTheSeededPaletteColours()
    {
        Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.Black)).Should().Be("#000000");
        Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.Red)).Should().Be("#FF0000");
        Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.Yellow)).Should().Be("#FFFF00");
        Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.White)).Should().Be("#FFFFFF");
    }

    [Fact]
    public void Detect_ResolvesAnIndexedColour()
    {
        // Index 2 is red in the OOXML indexed palette.
        var hex = Detect(sheet => SetSolid(sheet.Cell(1, 1), XLColor.FromIndex(2)));

        hex.Should().Be("#FF0000");
    }

    [Fact]
    public void Detect_ResolvesAThemeColour()
    {
        // Accent 1 in the default Office theme; the fill carries no tint, so the
        // resolved value must equal the theme colour itself.
        var hex = Detect(sheet =>
        {
            var cell = sheet.Cell(1, 1);
            SetSolid(cell, sheet.Workbook.Theme.Accent1);
        });

        hex.Should().Be("#4F81BD");
    }

    [Fact]
    public void Detect_AlwaysReturnsAWellFormedHex_ForEveryFillItCanSee()
    {
        var colours = new[]
        {
            XLColor.Black, XLColor.White, XLColor.FromArgb(1, 2, 3),
            XLColor.FromIndex(0), XLColor.FromIndex(63)
        };

        foreach (var colour in colours)
        {
            var hex = Detect(sheet => SetSolid(sheet.Cell(1, 1), colour));
            hex.Should().MatchRegex("^#[0-9A-F]{6}$");
        }
    }
}