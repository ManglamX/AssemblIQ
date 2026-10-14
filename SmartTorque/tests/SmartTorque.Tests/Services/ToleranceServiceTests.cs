using FluentAssertions;

using SmartTorque.Core.Enums;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

/// <summary>
/// Unit tests for ToleranceService per P2.2 requirements.
/// Covers: absolute mode, percent mode, boundary conditions, rounding.
/// </summary>
public class ToleranceServiceTests
{
    private readonly ToleranceService _sut = new();

    // -------------------------------------------------------
    // Absolute mode — ComputeBand
    // -------------------------------------------------------

    [Fact]
    public void ComputeBand_Absolute_ReturnsCorrectBounds()
    {
        var (lower, upper) = _sut.ComputeBand(100m, 2m, 3m, ToleranceMode.Absolute);
        lower.Should().Be(98m);
        upper.Should().Be(103m);
    }

    [Fact]
    public void ComputeBand_Absolute_ZeroTolerance_ReturnsSinglePoint()
    {
        var (lower, upper) = _sut.ComputeBand(50m, 0m, 0m, ToleranceMode.Absolute);
        lower.Should().Be(50m);
        upper.Should().Be(50m);
    }

    [Fact]
    public void ComputeBand_Absolute_RoundsTo3DecimalPlaces()
    {
        // target=10, minus=0.00049, plus=0.00049  →  both should round to 10.000
        var (lower, upper) = _sut.ComputeBand(10m, 0.00049m, 0.00049m, ToleranceMode.Absolute);
        lower.Should().Be(10.000m);
        upper.Should().Be(10.000m);
    }

    [Fact]
    public void ComputeBand_Absolute_MidpointRoundsAwayFromZero()
    {
        // 10 - 0.0005 = 9.9995 → rounds to 10.000 (AwayFromZero)
        var (lower, _) = _sut.ComputeBand(10m, 0.0005m, 0m, ToleranceMode.Absolute);
        lower.Should().Be(10.000m);
    }

    // -------------------------------------------------------
    // Percent mode — ComputeBand
    // -------------------------------------------------------

    [Fact]
    public void ComputeBand_Percent_ReturnsCorrectBounds()
    {
        // target=100, ±5%  →  [95, 105]
        var (lower, upper) = _sut.ComputeBand(100m, 5m, 5m, ToleranceMode.Percent);
        lower.Should().Be(95m);
        upper.Should().Be(105m);
    }

    [Fact]
    public void ComputeBand_Percent_AsymmetricTolerance()
    {
        // target=50, minus=10%, plus=20%  →  [45, 60]
        var (lower, upper) = _sut.ComputeBand(50m, 10m, 20m, ToleranceMode.Percent);
        lower.Should().Be(45m);
        upper.Should().Be(60m);
    }

    [Fact]
    public void ComputeBand_Percent_SmallTargetRounding()
    {
        // target=1.234, ±1%  →  lower=1.234-0.01234=1.22166→1.222, upper=1.24634→1.246
        var (lower, upper) = _sut.ComputeBand(1.234m, 1m, 1m, ToleranceMode.Percent);
        lower.Should().Be(1.222m);
        upper.Should().Be(1.246m);
    }

    // -------------------------------------------------------
    // IsInBand — inclusive boundary checks
    // -------------------------------------------------------

    [Theory]
    [InlineData(98.0, true)]   // exactly lower bound
    [InlineData(103.0, true)]   // exactly upper bound
    [InlineData(100.5, true)]   // within
    [InlineData(97.999, false)] // just below lower
    [InlineData(103.001, false)] // just above upper
    public void IsInBand_Absolute_BoundaryConditions(double reading, bool expected)
    {
        // target=100, minus=2, plus=3  →  [98, 103]
        var result = _sut.IsInBand((decimal)reading, 100m, 2m, 3m, ToleranceMode.Absolute);
        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(95.0, true)]   // exactly lower bound
    [InlineData(105.0, true)]   // exactly upper bound
    [InlineData(94.999, false)] // just below
    [InlineData(105.001, false)] // just above
    public void IsInBand_Percent_BoundaryConditions(double reading, bool expected)
    {
        // target=100, ±5%  →  [95, 105]
        var result = _sut.IsInBand((decimal)reading, 100m, 5m, 5m, ToleranceMode.Percent);
        result.Should().Be(expected);
    }

    [Fact]
    public void IsInBand_ReadingRoundedBeforeComparison()
    {
        // reading=97.9995 rounds to 98.000 which is exactly on lower bound → should pass
        var result = _sut.IsInBand(97.9995m, 100m, 2m, 3m, ToleranceMode.Absolute);
        result.Should().BeTrue("reading rounds to exactly 98.000 which is the lower bound");
    }
}