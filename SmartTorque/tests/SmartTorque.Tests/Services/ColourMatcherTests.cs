using FluentAssertions;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class ColourMatcherTests
{
    private static PriorityColourMap Shade(string hex, PriorityLevel priority, int tolerance = 40)
        => new()
        {
            ColourHex = hex,
            Priority = priority,
            Label = priority.ToString(),
            MatchTolerance = tolerance,
            IsActive = true
        };

    /// <summary>The seeded palette from Architecture §5.3.</summary>
    private static List<PriorityColourMap> Palette() =>
    [
        Shade("#000000", PriorityLevel.Black),
        Shade("#FF0000", PriorityLevel.Red),
        Shade("#FFFF00", PriorityLevel.Yellow),
        Shade("#00B050", PriorityLevel.Green),
        Shade("#FFFFFF", PriorityLevel.White)
    ];

    // -----------------------------------------------------------------------
    // Distance
    // -----------------------------------------------------------------------

    [Fact]
    public void Distance_IsZeroForIdenticalColours()
    {
        ColourMatcher.Distance("#123ABC", "#123ABC").Should().Be(0);
    }

    [Fact]
    public void Distance_IsCaseInsensitive()
    {
        ColourMatcher.Distance("#123abc", "#123ABC").Should().Be(0);
    }

    [Theory]
    [InlineData("#000000", "#FFFFFF", 441.6729)] // sqrt(3 * 255^2)
    [InlineData("#FF0000", "#FE0000", 1.0)]
    [InlineData("#FF0000", "#00FF00", 360.6245)] // sqrt(2 * 255^2)
    public void Distance_IsEuclideanInRgb(string a, string b, double expected)
    {
        ColourMatcher.Distance(a, b).Should().BeApproximately(expected, 0.01);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("123456")]
    [InlineData("#12345")]
    [InlineData("#GGGGGG")]
    public void Distance_IsInfiniteForUnreadableColours(string? hex)
    {
        double.IsPositiveInfinity(ColourMatcher.Distance(hex!, "#000000")).Should().BeTrue();
    }

    // -----------------------------------------------------------------------
    // Resolve
    // -----------------------------------------------------------------------

    [Fact]
    public void Resolve_MatchesAnExactShade()
    {
        var match = ColourMatcher.Resolve("#FF0000", Palette());

        match.WithinTolerance.Should().BeTrue();
        match.Priority.Should().Be(PriorityLevel.Red);
        match.Shade!.Label.Should().Be("Red");
    }

    [Fact]
    public void Resolve_MatchesANearShadeWithinTolerance()
    {
        // Distance from black is sqrt(3 * 10^2) ~= 17.3, well inside the tolerance of 40.
        var match = ColourMatcher.Resolve("#0A0A0A", Palette());

        match.WithinTolerance.Should().BeTrue();
        match.Priority.Should().Be(PriorityLevel.Black);
    }

    [Fact]
    public void Resolve_FallsBackToWhite_WhenNothingIsWithinTolerance()
    {
        // Distance from black is sqrt(3 * 48^2) ~= 83, outside the tolerance of 40.
        var match = ColourMatcher.Resolve("#303030", Palette());

        match.WithinTolerance.Should().BeFalse();
        match.Priority.Should().Be(PriorityLevel.White);
    }

    [Fact]
    public void Resolve_PicksTheNearestShade_WhenTwoAreWithinTolerance()
    {
        var map = new List<PriorityColourMap>
        {
            Shade("#000000", PriorityLevel.Black),
            Shade("#202020", PriorityLevel.Red)
        };

        // 17.3 from black, 38.1 from #202020 — both inside tolerance.
        var match = ColourMatcher.Resolve("#0A0A0A", map);

        match.Shade!.Priority.Should().Be(PriorityLevel.Black);
    }

    [Fact]
    public void Resolve_RespectsEachShadesOwnTolerance()
    {
        var map = new List<PriorityColourMap>
        {
            Shade("#000000", PriorityLevel.Black, tolerance: 5),
            Shade("#FFFFFF", PriorityLevel.White)
        };

        // #050505 is sqrt(3 * 5^2) ~= 8.66 away — outside this shade's tighter tolerance.
        ColourMatcher.Resolve("#050505", map).WithinTolerance.Should().BeFalse();

        // #020202 is sqrt(3 * 2^2) ~= 3.46 away — inside it.
        ColourMatcher.Resolve("#020202", map).WithinTolerance.Should().BeTrue();
    }

    [Fact]
    public void Resolve_TreatsAMissingColourAsWhite()
    {
        var match = ColourMatcher.Resolve(null, Palette());

        match.WithinTolerance.Should().BeTrue(); // White is in the map at distance 0.
        match.Priority.Should().Be(PriorityLevel.White);
    }

    [Fact]
    public void Resolve_ReturnsWhitePriority_WhenTheMapHasNoWhiteShade()
    {
        var map = new List<PriorityColourMap> { Shade("#000000", PriorityLevel.Black) };

        var match = ColourMatcher.Resolve("#303030", map);

        match.Shade.Should().BeNull();
        match.Priority.Should().Be(PriorityLevel.White);
    }

    [Fact]
    public void Resolve_ReturnsNothing_ForAnEmptyMap()
    {
        var match = ColourMatcher.Resolve("#000000", []);

        match.Shade.Should().BeNull();
        match.WithinTolerance.Should().BeFalse();
        match.Priority.Should().Be(PriorityLevel.White);
    }

    [Fact]
    public void Resolve_MatchesBlueShade()
    {
        var map = new List<PriorityColourMap>
        {
            Shade("#000000", PriorityLevel.Black),
            Shade("#FF0000", PriorityLevel.Red),
            Shade("#0070C0", PriorityLevel.Blue, tolerance: 60)
        };

        var match = ColourMatcher.Resolve("#0070C0", map);

        match.WithinTolerance.Should().BeTrue();
        match.Priority.Should().Be(PriorityLevel.Blue);
    }
}