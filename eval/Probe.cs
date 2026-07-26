using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper: dumps the full raw question JSON from the detail endpoint
/// (with_cp) for one resolved binary question, so we can see every field the API
/// actually exposes — resolution, aggregations, my_forecasts. One or two requests.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();

        // Find a binary resolved post id from the list.
        var list = await client.ListPostsAsync(tournamentId, "resolved", limit: 60);
        long? target = null;
        if (list.TryGetProperty("results", out var results) &&
            results.ValueKind == JsonValueKind.Array)
        {
            foreach (var post in results.EnumerateArray())
            {
                if (post.TryGetProperty("question", out var q) &&
                    q.TryGetProperty("type", out var t) && t.GetString() == "binary")
                {
                    target = post.GetProperty("id").GetInt64();
                    break;
                }
            }
        }
        if (target is null)
        {
            Console.WriteLine("No binary resolved post found.");
            return 0;
        }

        Console.WriteLine($"== Full detail (with_cp) for binary post {target} ==");
        var detail = await client.GetPostAsync(target.Value);
        if (!detail.TryGetProperty("question", out var question))
        {
            Console.WriteLine("No question in detail.");
            return 0;
        }

        Console.WriteLine("resolution         : " + Raw(question, "resolution"));
        Console.WriteLine("actual_resolve_time: " + Raw(question, "actual_resolve_time"));
        Console.WriteLine("\n-- full question JSON (truncated 6000) --");
        Console.WriteLine(Truncate(question.GetRawText(), 6000));
        return 0;
    }

    private static string Raw(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.GetRawText() : "(absent)";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
