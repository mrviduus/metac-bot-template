namespace PredictKitEval;

/// <summary>
/// A single scored data point: a probability assigned to a binary outcome that
/// has since resolved. Probability is P(Yes); Outcome is 1 for Yes, 0 for No.
/// </summary>
public readonly record struct BinaryPoint(double Probability, int Outcome)
{
    public double ClampedProbability => Math.Clamp(Probability, 1e-6, 1 - 1e-6);
}

public sealed record CalibrationBin(
    double LowerEdge,
    double UpperEdge,
    int Count,
    double MeanPredicted,
    double ObservedFrequency);

public sealed record ScoreSummary(
    int Count,
    double BrierScore,
    double LogScore,
    double ExpectedCalibrationError,
    IReadOnlyList<CalibrationBin> Bins);

/// <summary>
/// Pure scoring functions for binary forecasts. No I/O — every method is a
/// deterministic transform of the point set, so it's trivially testable.
/// </summary>
public static class Scoring
{
    /// <summary>Mean Brier score: average of (p - outcome)^2. Range [0,1], lower is better.</summary>
    public static double BrierScore(IReadOnlyList<BinaryPoint> points)
    {
        if (points.Count == 0) return double.NaN;
        double sum = 0;
        foreach (var pt in points)
        {
            double diff = pt.Probability - pt.Outcome;
            sum += diff * diff;
        }
        return sum / points.Count;
    }

    /// <summary>Mean negative log score: -mean(log p_assigned_to_actual_outcome). Lower is better.</summary>
    public static double LogScore(IReadOnlyList<BinaryPoint> points)
    {
        if (points.Count == 0) return double.NaN;
        double sum = 0;
        foreach (var pt in points)
        {
            double p = pt.ClampedProbability;
            double pForOutcome = pt.Outcome == 1 ? p : 1 - p;
            sum += -Math.Log(pForOutcome);
        }
        return sum / points.Count;
    }

    /// <summary>
    /// Bins predictions by P(Yes) into equal-width buckets and, per bin, reports
    /// mean predicted probability vs observed Yes-frequency. Empty bins are dropped.
    /// </summary>
    public static IReadOnlyList<CalibrationBin> CalibrationBins(
        IReadOnlyList<BinaryPoint> points, int binCount = 10)
    {
        if (binCount < 1) throw new ArgumentOutOfRangeException(nameof(binCount));

        var predictedSum = new double[binCount];
        var outcomeSum = new int[binCount];
        var count = new int[binCount];

        foreach (var pt in points)
        {
            // p == 1.0 lands in the last bin rather than an out-of-range index.
            int idx = Math.Min((int)(pt.Probability * binCount), binCount - 1);
            idx = Math.Max(idx, 0);
            predictedSum[idx] += pt.Probability;
            outcomeSum[idx] += pt.Outcome;
            count[idx]++;
        }

        var bins = new List<CalibrationBin>();
        for (int i = 0; i < binCount; i++)
        {
            if (count[i] == 0) continue;
            bins.Add(new CalibrationBin(
                LowerEdge: (double)i / binCount,
                UpperEdge: (double)(i + 1) / binCount,
                Count: count[i],
                MeanPredicted: predictedSum[i] / count[i],
                ObservedFrequency: (double)outcomeSum[i] / count[i]));
        }
        return bins;
    }

    /// <summary>
    /// Expected Calibration Error: count-weighted mean of |mean predicted − observed|
    /// across bins. 0 is perfectly calibrated.
    /// </summary>
    public static double ExpectedCalibrationError(
        IReadOnlyList<BinaryPoint> points, int binCount = 10)
    {
        if (points.Count == 0) return double.NaN;
        double weighted = 0;
        foreach (var bin in CalibrationBins(points, binCount))
        {
            weighted += bin.Count * Math.Abs(bin.MeanPredicted - bin.ObservedFrequency);
        }
        return weighted / points.Count;
    }

    public static ScoreSummary Summarize(IReadOnlyList<BinaryPoint> points, int binCount = 10)
    {
        return new ScoreSummary(
            Count: points.Count,
            BrierScore: BrierScore(points),
            LogScore: LogScore(points),
            ExpectedCalibrationError: ExpectedCalibrationError(points, binCount),
            Bins: CalibrationBins(points, binCount));
    }
}
