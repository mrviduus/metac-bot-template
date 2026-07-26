namespace PredictKitEval;

/// <summary>
/// Loads a tournament's scoreable binary questions and prints the bot's scores
/// next to the community's on the same question set.
/// </summary>
internal static class Report
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Scoring PredictKit on binary questions in '{tournamentId}' ==");

        var records = await Dataset.LoadBinaryAsync(client, tournamentId);
        Console.WriteLine($"Scoreable binary questions (resolved + bot forecast): {records.Count}");
        if (records.Count == 0)
        {
            Console.WriteLine(
                "\nNothing to score yet. This is expected while the bot is young: a " +
                "question counts only once it has both resolved AND been forecasted by " +
                "PredictKit. Re-run as more forecasted questions resolve.");
            return 0;
        }

        var botPoints = records
            .Select(r => new BinaryPoint(r.BotProbability, r.Outcome)).ToList();
        var botSummary = Scoring.Summarize(botPoints);

        var withCommunity = records.Where(r => r.CommunityProbability is not null).ToList();
        ScoreSummary? communitySummary = withCommunity.Count > 0
            ? Scoring.Summarize(withCommunity
                .Select(r => new BinaryPoint(r.CommunityProbability!.Value, r.Outcome)).ToList())
            : null;

        Console.WriteLine();
        PrintRow("metric", "PredictKit", "community");
        PrintRow("n", botSummary.Count.ToString(),
            (communitySummary?.Count ?? 0).ToString());
        PrintRow("Brier (lower=better)", F(botSummary.BrierScore),
            communitySummary is null ? "-" : F(communitySummary.BrierScore));
        PrintRow("Log (lower=better)", F(botSummary.LogScore),
            communitySummary is null ? "-" : F(communitySummary.LogScore));
        PrintRow("ECE (lower=better)", F(botSummary.ExpectedCalibrationError),
            communitySummary is null ? "-" : F(communitySummary.ExpectedCalibrationError));

        if (communitySummary is not null)
        {
            double edge = communitySummary.BrierScore - botSummary.BrierScore;
            Console.WriteLine($"\nBrier edge vs community: {edge:+0.0000;-0.0000} " +
                              $"({(edge >= 0 ? "bot ahead" : "community ahead")})");
        }

        Console.WriteLine("\nPer-question:");
        foreach (var r in records.OrderBy(r => r.PostId))
        {
            string cp = r.CommunityProbability is null ? "  -  " : $"{r.CommunityProbability:F2}";
            Console.WriteLine($"  [{(r.Outcome == 1 ? "YES" : "NO ")}] bot {r.BotProbability:F2} " +
                              $"cp {cp}  #{r.PostId} {Truncate(r.Title, 60)}");
        }
        return 0;
    }

    private static void PrintRow(string a, string b, string c) =>
        Console.WriteLine($"  {a,-22} {b,12} {c,12}");

    private static string F(double v) => double.IsNaN(v) ? "-" : v.ToString("F4");

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
