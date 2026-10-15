using FluentAssertions;

using SmartTorque.Core.Enums;
using SmartTorque.Services;

using Xunit;

namespace SmartTorque.Tests.Services;

public class TemplateCodeGeneratorTests
{
    [Fact]
    public void Generate_TorqueWrench_ProducesCorrectCode_WithAbsoluteTolerance()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Torque, 25.0m, 2.0m, 2.0m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 25.0m, 2.0m, 2.0m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 25.0m, 2.0m, 2.0m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 25.0m, 2.0m, 2.0m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Torque, steps);

        code.Should().Be("TW-4B-25NM-T2");
    }

    [Fact]
    public void Generate_TorqueWrench_ProducesCorrectCode_WithDecimalValues()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Torque, 30.5m, 1.5m, 1.5m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 30.5m, 1.5m, 1.5m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Torque, steps);

        code.Should().Be("TW-2B-30.5NM-T1.5");
    }

    [Fact]
    public void Generate_TorqueWrench_ProducesCorrectCode_WithPercentTolerance()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Torque, 50.0m, 5.0m, 5.0m, ToleranceMode.Percent)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Torque, steps);

        code.Should().Be("TW-1B-50NM-T5P");
    }

    [Fact]
    public void Generate_TorqueWrench_ProducesCorrectCode_WithAsymmetricTolerance()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Torque, 20.0m, 2.0m, 1.0m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Torque, steps);

        code.Should().Be("TW-1B-20NM-TP2M1");
    }

    [Fact]
    public void Generate_VernierCaliper_ProducesCorrectCode_SingleSide()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Vernier, 850.0m, 1.0m, 1.0m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Vernier, steps);

        code.Should().Be("VC-1S-850MM-T1");
    }

    [Fact]
    public void Generate_VernierCaliper_ProducesCorrectCode_MultipleSidesWithDecimals()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Vernier, 100.0m, 0.5m, 0.5m, ToleranceMode.Absolute),
            new(InstrumentType.Vernier, 100.0m, 0.5m, 0.5m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Vernier, steps);

        code.Should().Be("VC-2S-100MM-T0.5");
    }

    [Fact]
    public void Generate_MixedMode_CombinesTorqueAndVernierSpecifications()
    {
        var steps = new List<TemplateCodeGenerator.StepSpec>
        {
            new(InstrumentType.Torque, 18.0m, 1.5m, 1.5m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 18.0m, 1.5m, 1.5m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 18.0m, 1.5m, 1.5m, ToleranceMode.Absolute),
            new(InstrumentType.Torque, 18.0m, 1.5m, 1.5m, ToleranceMode.Absolute),
            new(InstrumentType.Vernier, 850.0m, 1.0m, 1.0m, ToleranceMode.Absolute)
        };

        var code = TemplateCodeGenerator.Generate(TemplateMode.Mixed, steps);

        code.Should().Be("TW-4B-18NM-T1.5-VC-1S-850MM-T1");
        code.Length.Should().BeLessThanOrEqualTo(50);
    }

    [Fact]
    public void Generate_EmptySteps_ReturnsFallbackPrefix()
    {
        TemplateCodeGenerator.Generate(TemplateMode.Torque, Array.Empty<TemplateCodeGenerator.StepSpec>())
            .Should().Be("TW-0B");

        TemplateCodeGenerator.Generate(TemplateMode.Vernier, Array.Empty<TemplateCodeGenerator.StepSpec>())
            .Should().Be("VC-0S");

        TemplateCodeGenerator.Generate(TemplateMode.Mixed, Array.Empty<TemplateCodeGenerator.StepSpec>())
            .Should().Be("TPL-GEN");
    }

    [Fact]
    public void FormatNumber_FormatsIntegersAndDecimalsConsistently()
    {
        TemplateCodeGenerator.FormatNumber(25.0m).Should().Be("25");
        TemplateCodeGenerator.FormatNumber(25.5m).Should().Be("25.5");
        TemplateCodeGenerator.FormatNumber(0.50m).Should().Be("0.5");
        TemplateCodeGenerator.FormatNumber(850.000m).Should().Be("850");
    }
}