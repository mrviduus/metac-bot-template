namespace PredictKitEval;

/// <summary>
/// Hand-checked assertions on the scoring math. Runs without any network access,
/// so the core is verifiable before the Metaculus API client is wired in.
/// </summary>
internal static class SelfTest
{
    private const double Eps = 1e-9;

    public static int Run()
    {
        int failures = 0;

        // Perfect forecasts: p=1 on Yes, p=0 on No -> Brier 0, ECE 0.
        var perfect = new BinaryPoint[] { new(1.0, 1), new(0.0, 0), new(1.0, 1) };
        failures += Check("Brier perfect", Scoring.BrierScore(perfect), 0.0);
        failures += Check("ECE perfect", Scoring.ExpectedCalibrationError(perfect), 0.0);

        // Worst forecasts: p=1 on No, p=0 on Yes -> Brier 1.
        var worst = new BinaryPoint[] { new(1.0, 0), new(0.0, 1) };
        failures += Check("Brier worst", Scoring.BrierScore(worst), 1.0);

        // Always 0.5 -> Brier 0.25 regardless of outcomes.
        var half = new BinaryPoint[] { new(0.5, 1), new(0.5, 0), new(0.5, 1), new(0.5, 0) };
        failures += Check("Brier 0.5", Scoring.BrierScore(half), 0.25);

        // Log score for constant 0.5 = -ln(0.5) = ln 2.
        failures += Check("Log 0.5", Scoring.LogScore(half), Math.Log(2));

        // Calibration: 10 points at p=0.7, 7 resolve Yes -> observed freq 0.7, ECE 0.
        var calibrated = new List<BinaryPoint>();
        for (int i = 0; i < 10; i++) calibrated.Add(new BinaryPoint(0.7, i < 7 ? 1 : 0));
        failures += Check("ECE calibrated 0.7", Scoring.ExpectedCalibrationError(calibrated), 0.0);

        // Overconfident: 10 points at p=0.9 but only 5 Yes -> ECE = |0.9-0.5| = 0.4.
        var overconfident = new List<BinaryPoint>();
        for (int i = 0; i < 10; i++) overconfident.Add(new BinaryPoint(0.9, i < 5 ? 1 : 0));
        failures += Check("ECE overconfident", Scoring.ExpectedCalibrationError(overconfident), 0.4);

        // p=1.0 must land in the last bin, not throw.
        var edge = new BinaryPoint[] { new(1.0, 1) };
        failures += Check("edge p=1 bin count", Scoring.CalibrationBins(edge).Count, 1);

        Console.WriteLine(failures == 0
            ? "SelfTest: all checks passed."
            : $"SelfTest: {failures} check(s) FAILED.");
        return failures == 0 ? 0 : 1;
    }

    private static int Check(string name, double actual, double expected)
    {
        bool ok = Math.Abs(actual - expected) < Eps;
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {name}: got {actual:F6}, want {expected:F6}");
        return ok ? 0 : 1;
    }
}
