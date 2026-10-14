using SmartTorque.Core.Enums;
using SmartTorque.Core.Interfaces;

namespace SmartTorque.Services;

/// <summary>
/// Evaluates measurement readings against configured tolerance bands.
/// Rules:
///   - Absolute mode: band = [target - toleranceMinus, target + tolerancePlus]
///   - Percent mode:  band = [target - target*toleranceMinus/100, target + target*tolerancePlus/100]
///   - All values rounded to 3 decimal places using MidpointRounding.AwayFromZero (Architecture §7.1)
///   - Bounds are inclusive
/// </summary>
public sealed class ToleranceService : IMeasurementEvaluator
{
    private const int DecimalPlaces = 3;
    private static readonly MidpointRounding Rounding = MidpointRounding.AwayFromZero;

    public (decimal Lower, decimal Upper) ComputeBand(
        decimal target,
        decimal toleranceMinus,
        decimal tolerancePlus,
        ToleranceMode mode)
    {
        decimal lower, upper;

        if (mode == ToleranceMode.Absolute)
        {
            lower = target - toleranceMinus;
            upper = target + tolerancePlus;
        }
        else // Percent
        {
            lower = target - target * toleranceMinus / 100m;
            upper = target + target * tolerancePlus / 100m;
        }

        return (
            Math.Round(lower, DecimalPlaces, Rounding),
            Math.Round(upper, DecimalPlaces, Rounding)
        );
    }

    public bool IsInBand(
        decimal reading,
        decimal target,
        decimal toleranceMinus,
        decimal tolerancePlus,
        ToleranceMode mode)
    {
        var roundedReading = Math.Round(reading, DecimalPlaces, Rounding);
        var (lower, upper) = ComputeBand(target, toleranceMinus, tolerancePlus, mode);
        return roundedReading >= lower && roundedReading <= upper;
    }
}