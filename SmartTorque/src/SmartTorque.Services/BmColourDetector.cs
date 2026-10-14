using System.Drawing;

using ClosedXML.Excel;

namespace SmartTorque.Services;

/// <summary>
/// Resolves the fill of a BM product-code cell to a <c>#RRGGBB</c> value
/// (PRD BM-4, Architecture §7.3 step 4). Covers solid RGB, theme colours with tint,
/// and indexed palette colours. An unfilled cell is White.
/// </summary>
public static class BmColourDetector
{
    /// <summary>What a cell with no fill resolves to — PRD BM-4 treats no fill as White.</summary>
    public const string DefaultWhite = "#FFFFFF";

    /// <summary>
    /// The fill of <paramref name="cell"/> as <c>#RRGGBB</c>, never throwing —
    /// an unreadable fill degrades to White rather than failing the whole import.
    /// </summary>
    public static string? Detect(IXLWorkbook workbook, IXLCell? cell)
    {
        if (workbook == null || cell == null) return null;

        try
        {
            var fill = cell.Style.Fill;

            // 4.4 No fill at all means White.
            if (fill.PatternType != XLFillPatternValues.Solid)
                return DefaultWhite;

            var colour = fill.BackgroundColor;
            if (!colour.HasValue)
                return DefaultWhite;

            switch (colour.ColorType)
            {
                // 4.1 Already RGB.
                case XLColorType.Color:
                    return ToHex(colour.Color);

                // 4.2 A theme colour, with the tint applied per OOXML.
                case XLColorType.Theme:
                    {
                        var baseColour = workbook.Theme.ResolveThemeColor(colour.ThemeColor);
                        if (!baseColour.HasValue || baseColour.ColorType != XLColorType.Color)
                            return DefaultWhite;

                        return ToHex(ApplyTint(baseColour.Color, colour.ThemeTint));
                    }

                // 4.3 An indexed palette colour.
                case XLColorType.Indexed:
                    return IndexedColour(colour.Indexed) ?? DefaultWhite;

                default:
                    return DefaultWhite;
            }
        }
        catch (Exception)
        {
            return DefaultWhite;
        }
    }

    /// <summary>
    /// ECMA-376 tint: 0 leaves the channel unchanged, 0.5 drives it to white and 1 to black.
    /// Recorded as an assumption under D-036.
    /// </summary>
    public static byte ApplyTint(byte channel, double tint)
    {
        var t = Math.Clamp(tint, 0d, 1d);

        if (t < 0.5)
        {
            var lightening = t * 2d;
            return (byte)Math.Round(channel * (1d - lightening) + 255d * lightening);
        }

        var darkening = (1d - t) * 2d;
        return (byte)Math.Round(channel * darkening);
    }

    public static string ToHex(byte r, byte g, byte b)
        => FormattableString.Invariant($"#{r:X2}{g:X2}{b:X2}");

    public static string ToHex(Color colour) => ToHex(colour.R, colour.G, colour.B);

    /// <summary>Tints a whole colour, channel by channel.</summary>
    public static Color ApplyTint(Color colour, double tint)
        => Color.FromArgb(
            ApplyTint(colour.R, tint),
            ApplyTint(colour.G, tint),
            ApplyTint(colour.B, tint));

    private static string? IndexedColour(int index)
    {
        if (XLColor.IndexedColors == null) return null;
        if (!XLColor.IndexedColors.TryGetValue(index, out var colour)) return null;
        if (!colour.HasValue || colour.ColorType != XLColorType.Color) return null;

        return ToHex(colour.Color);
    }
}