using System.Globalization;

using SmartTorque.Core.Entities;
using SmartTorque.Core.Enums;

namespace SmartTorque.Services;

/// <summary>
/// Uniquely generates deterministic template codes from instrument specifications:
/// - Wrench (Torque): number of bolts, target torque (N·m), and tolerance (e.g. TW-4B-25NM-T2).
/// - Caliper (Vernier): number of sides, target dimensions (mm), and tolerance in mm (e.g. VC-2S-100MM-T0.5).
/// - Mixed: combines both instrument specifications.
/// </summary>
public static class TemplateCodeGenerator
{
    public record StepSpec(
        InstrumentType Instrument,
        decimal Target,
        decimal TolerancePlus,
        decimal ToleranceMinus,
        ToleranceMode ToleranceMode);

    public static string Generate(TemplateMode mode, IEnumerable<StepSpec> steps)
    {
        var stepList = steps?.ToList() ?? [];
        if (stepList.Count == 0)
        {
            return mode switch
            {
                TemplateMode.Torque => "TW-0B",
                TemplateMode.Vernier => "VC-0S",
                _ => "TPL-GEN"
            };
        }

        var torqueSteps = stepList.Where(s => s.Instrument == InstrumentType.Torque).ToList();
        var vernierSteps = stepList.Where(s => s.Instrument == InstrumentType.Vernier).ToList();

        string? torquePart = torqueSteps.Count > 0 ? GenerateTorquePart(torqueSteps) : null;
        string? vernierPart = vernierSteps.Count > 0 ? GenerateVernierPart(vernierSteps) : null;

        if (torquePart != null && vernierPart != null)
        {
            var combined = $"{torquePart}-{vernierPart}";
            return ClampCode(combined);
        }

        if (torquePart != null)
        {
            return ClampCode(torquePart);
        }

        if (vernierPart != null)
        {
            return ClampCode(vernierPart);
        }

        return "TPL-GEN";
    }

    public static string Generate(TemplateMode mode, IEnumerable<StepRequest> steps) =>
        Generate(mode, steps.Select(s => new StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)));

    public static string Generate(TemplateMode mode, IEnumerable<TemplateStep> steps) =>
        Generate(mode, steps.Select(s => new StepSpec(
            s.Instrument, s.Target, s.TolerancePlus, s.ToleranceMinus, s.ToleranceMode)));

    private static string GenerateTorquePart(List<StepSpec> steps)
    {
        int boltCount = steps.Count;
        var targets = steps.Select(s => FormatNumber(s.Target)).Distinct().ToList();
        var targetStr = string.Join("_", targets);
        var tol = FormatTolerance(steps);

        return $"TW-{boltCount}B-{targetStr}NM-{tol}";
    }

    private static string GenerateVernierPart(List<StepSpec> steps)
    {
        int sideCount = steps.Count;
        var dimensions = steps.Select(s => FormatNumber(s.Target)).Distinct().ToList();
        var dimStr = string.Join("_", dimensions);
        var tol = FormatTolerance(steps);

        return $"VC-{sideCount}S-{dimStr}MM-{tol}";
    }

    private static string FormatTolerance(List<StepSpec> steps)
    {
        var first = steps[0];
        if (first.ToleranceMode == ToleranceMode.Percent)
        {
            return $"T{FormatNumber(first.TolerancePlus)}P";
        }

        if (first.TolerancePlus == first.ToleranceMinus)
        {
            return $"T{FormatNumber(first.TolerancePlus)}";
        }

        return $"TP{FormatNumber(first.TolerancePlus)}M{FormatNumber(first.ToleranceMinus)}";
    }

    public static string FormatNumber(decimal value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (rounded == Math.Floor(rounded))
        {
            return rounded.ToString("0", CultureInfo.InvariantCulture);
        }

        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static string ClampCode(string code)
    {
        var upper = code.ToUpperInvariant();
        return upper.Length > 50 ? upper[..50] : upper;
    }
}