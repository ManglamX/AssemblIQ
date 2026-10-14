using SmartTorque.Core.Enums;

namespace SmartTorque.Core.Interfaces;

/// <summary>
/// Evaluates whether a measurement reading falls within the acceptance band.
/// </summary>
public interface IMeasurementEvaluator
{
    /// <summary>
    /// Computes the lower and upper acceptance bounds for a step.
    /// </summary>
    (decimal Lower, decimal Upper) ComputeBand(
        decimal target,
        decimal toleranceMinus,
        decimal tolerancePlus,
        ToleranceMode mode);

    /// <summary>
    /// Returns true if the reading is within the acceptance band (inclusive).
    /// </summary>
    bool IsInBand(
        decimal reading,
        decimal target,
        decimal toleranceMinus,
        decimal tolerancePlus,
        ToleranceMode mode);
}