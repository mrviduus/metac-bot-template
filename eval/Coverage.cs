using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Diagnostic: for both open and resolved questions, counts how many carry a
/// bot forecast (my_forecasts.latest) and a yes/no resolution. Distinguishes
/// "too early to score" (bot forecasts exist on open questions, none resolved
/// yet) from a parsing/token problem (no bot forecasts anywhere).
/// </summary>
internal static class Coverage
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Coverage for tournament '{tournamentId}' ==\n");

        foreach (var status in new[] { "open", "resolved" })
        {
            int total = 0, binary = 0, withBot = 0, withYesNo = 0, scoreable = 0;
            string? sampleForecast = null;

            int offset = 0;
            const int page = 60;
            while (true)
            {
                var list = await client.ListPostsAsync(tournamentId, status, page, offset);
                if (!list.TryGetProperty("results", out var results) ||
                    results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                    break;

                foreach (var post in results.EnumerateArray())
                {
                    if (!post.TryGetProperty("question", out var q)) continue;
                    total++;
                    if (GetString(q, "type") != "binary") continue;
                    binary++;

                    bool bot = TryForecastYes(q, out double p);
                    if (bot) { withBot++; sampleForecast ??= $"{p:F3}"; }

                    string? r = GetString(q, "resolution");
                    bool yesno = r is "yes" or "no";
                    if (yesno) withYesNo++;
                    if (bot && yesno) scoreable++;
                }

                int returned = results.GetArrayLength();
                offset += returned;
                if (returned < page) break;
            }

            Console.WriteLine($"[{status}] total={total} binary={binary} " +
                              $"withBotForecast={withBot} withYesNoResolution={withYesNo} " +
                              $"scoreable={scoreable}" +
                              (sampleForecast is not null ? $" sampleBotP(Yes)={sampleForecast}" : ""));
        }

        Console.WriteLine("\nReading: bot forecasts on 'open' but scoreable=0 on 'resolved' " +
                          "=> harness works, just too early. Zero bot forecasts anywhere " +
                          "=> token/my_forecasts problem to investigate.");
        return 0;
    }

    private static bool TryForecastYes(JsonElement question, out double p)
    {
        p = 0;
        if (!question.TryGetProperty("my_forecasts", out var mine)) return false;
        if (!mine.TryGetProperty("latest", out var latest) ||
            latest.ValueKind != JsonValueKind.Object) return false;
        if (!latest.TryGetProperty("forecast_values", out var fv) ||
            fv.ValueKind != JsonValueKind.Array || fv.GetArrayLength() < 2) return false;
        p = fv[1].GetDouble();
        return true;
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
