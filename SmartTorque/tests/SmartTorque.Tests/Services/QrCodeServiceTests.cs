using FluentAssertions;

using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class QrCodeServiceTests
{
    private readonly QrCodeService _service = new();

    [Fact]
    public void GenerateSvg_WithValidToken_ReturnsNonEmptySvg()
    {
        var token = "PRD-E7CF37B10F7C1C8001E5439B7353AA633508";
        var svg = _service.GenerateSvg(token, 10);

        svg.Should().NotBeNullOrWhiteSpace();
        svg.Should().Contain("<svg");
        svg.Should().Contain("</svg>");
    }

    [Fact]
    public void GeneratePng_WithValidToken_ReturnsPngBytes()
    {
        var token = "PRD-E7CF37B10F7C1C8001E5439B7353AA633508";
        var bytes = _service.GeneratePng(token, 10);

        bytes.Should().NotBeNull();
        bytes.Length.Should().BeGreaterThan(0);
        // PNG header signature: 0x89, 'P', 'N', 'G'
        bytes[0].Should().Be(0x89);
        bytes[1].Should().Be(0x50);
        bytes[2].Should().Be(0x4E);
        bytes[3].Should().Be(0x47);
    }

    [Fact]
    public void GenerateSvg_WithEmptyToken_ReturnsEmptyString()
    {
        var svg = _service.GenerateSvg("");
        svg.Should().BeEmpty();
    }
}