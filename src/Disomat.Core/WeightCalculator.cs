namespace Disomat.Core;

public enum MeasurementMode { Rounded, Raw }
public sealed record WeightSample(decimal RawKg, int WholeKg, int RoundedKg);
public sealed record MeasurementResult(IReadOnlyList<WeightSample> Samples, decimal RawAverageKg,
    int RoundedAverageKg, decimal Tonnes, MeasurementMode Mode);

public static class WeightCalculator
{
    public static MeasurementResult Calculate(IEnumerable<decimal> readings, MeasurementMode mode = MeasurementMode.Rounded)
    {
        ArgumentNullException.ThrowIfNull(readings);
        var samples = readings.Select(weight =>
        {
            if (weight < 0 || weight > int.MaxValue - 20)
                throw new ArgumentOutOfRangeException(nameof(readings), "Unsupported weight range.");
            int whole = checked((int)Math.Round(weight, 0, MidpointRounding.ToEven));
            int rem = whole % 20;
            return new WeightSample(weight, whole, checked(whole - rem + (rem < 10 ? 0 : 20)));
        }).ToArray();
        if (samples.Length == 0) throw new ArgumentException("At least one sample is required.", nameof(readings));
        decimal rawAverage = samples.Average(x => x.RawKg);
        // Preserve historical integer division before conversion to tonnes.
        int roundedAverage = checked((int)(samples.Sum(x => (long)x.RoundedKg) / samples.Length));
        decimal effectiveKg = mode == MeasurementMode.Rounded ? roundedAverage : rawAverage;
        decimal tonnes = Math.Round(effectiveKg / 1000m, 2, MidpointRounding.ToEven);
        return new MeasurementResult(samples, rawAverage, roundedAverage, tonnes, mode);
    }
}
