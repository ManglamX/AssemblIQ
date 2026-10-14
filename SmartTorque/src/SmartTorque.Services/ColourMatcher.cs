using System.Globalization;
using System.Linq;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Services;

/// <summary>What a detected colour resolved to.</summary>
/// <param name="Shade">The shade to use, or null when the map has nothing to fall back to.</param>
/// <param name="WithinTolerance">True when a shade actually matched, rather than being defaulted.</param>
public record ColourMatch(PriorityColourMap? Shade, bool WithinTolerance)
{
    public PriorityLevel Priority => Shade?.Priority ?? PriorityLevel.White;
}

/// <summary>
/// Matches a detected BM cell colour to a priority shade (PRD BM-5, Architecture §7.3 step 4).
/// The nearest shade within its own match tolerance wins; otherwise the row falls back to White.
/// </summary>
public static class ColourMatcher
{
    /// <summary>Euclidean RGB distance between two <c>#RRGGBB</c> colours.</summary>
    public static double Distance(string hexA, string hexB)
    {
        if (!TryParse(hexA, out var a) || !TryParse(hexB, out var b))
            return double.PositiveInfinity;

        var dr = a.R - b.R;
        var dg = a.G - b.G;
        var db = a.B - b.B;

        return Math.Sqrt(dr * dr + dg * dg + db * db);
    }

    /// <summary>
    /// Resolves a colour against the active shades. An unreadable colour is treated exactly
    /// like no fill (White), so the caller can pass a null <paramref name="colourHex"/>.
    /// </summary>
    public static ColourMatch Resolve(string? colourHex, IReadOnlyList<PriorityColourMap> map)
    {
        if (map.Count == 0) return new ColourMatch(null, false);

        var target = string.IsNullOrWhiteSpace(colourHex)
            ? BmColourDetector.DefaultWhite
            : colourHex;

        PriorityColourMap? nearest = null;
        var nearestDistance = double.PositiveInfinity;

        foreach (var shade in map)
        {
            // Each shade carries its own tolerance (PRD BM-3).
            var distance = Distance(target, shade.ColourHex);
            if (distance <= shade.MatchTolerance && distance < nearestDistance)
            {
                nearest = shade;
                nearestDistance = distance;
            }
        }

        if (nearest != null) return new ColourMatch(nearest, true);

        // BM-5: nothing recognised means White.
        var white = map.FirstOrDefault(s => s.Priority == PriorityLevel.White);
        return new ColourMatch(white, false);
    }

    public static bool TryParse(string? hex, out (byte R, byte G, byte B) rgb)
    {
        rgb = default;

        if (string.IsNullOrWhiteSpace(hex)) return false;

        var value = hex.Trim();
        if (value.Length != 7 || value[0] != '#') return false;

        if (!byte.TryParse(value.AsSpan(1, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var r) ||
            !byte.TryParse(value.AsSpan(3, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var g) ||
            !byte.TryParse(value.AsSpan(5, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
        {
            return false;
        }

        rgb = (r, g, b);
        return true;
    }
}