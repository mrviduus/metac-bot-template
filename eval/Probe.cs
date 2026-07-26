using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper: finds one resolved binary question in a tournament and
/// dumps the raw resolution / community-aggregation / my_forecasts fields so the
/// dataset loader can be wired against the real JSON. Keeps request volume tiny
/// (one list page + one detail) to stay under Cloudflare's rate limit.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Inspecting resolved binary question in '{tournamentId}' ==");

        var list = await client.ListPostsAsync(tournamentId, "resolved", limit: 60);
        if (!list.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("No results.");
            return 0;
        }

        // Prefer a binary post; fall back to the first resolved post of any type.
        long? binaryId = null, anyId = null;
        foreach (var post in results.EnumerateArray())
        {
            long id = post.GetProperty("id").GetInt64();
            anyId ??= id;
            string? type = post.TryGetProperty("question", out var q) &&
                           q.TryGetProperty("type", out var t) ? t.GetString() : null;
            if (type == "binary") { binaryId = id; break; }
        }

        long target = binaryId ?? anyId ?? throw new InvalidOperationException("no posts");
        Console.WriteLine($"target post: {target} (binary={(binaryId is not null)})");

        var detail = await client.GetPostAsync(target);
        if (!detail.TryGetProperty("question", out var question))
        {
            Console.WriteLine("detail has no question.");
            return 0;
        }

        Console.WriteLine("\ntype        : " + Raw(question, "type"));
        Console.WriteLine("resolution  : " + Raw(question, "resolution"));
        Console.WriteLine("actual_resolve_time: " + Raw(question, "actual_resolve_time"));
        Console.WriteLine("\naggregations (full):");
        Console.WriteLine(Truncate(Raw(question, "aggregations"), 2500));
        Console.WriteLine("\nmy_forecasts (full):");
        Console.WriteLine(Truncate(Raw(question, "my_forecasts"), 2500));
        return 0;
    }

    private static string Raw(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.GetRawText() : "(absent)";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
