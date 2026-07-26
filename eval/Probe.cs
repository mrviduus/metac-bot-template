using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper: surveys resolved questions in a tournament and reports
/// what is actually scoreable — how many carry a resolution and a forecast from
/// this token's account — plus one fully-populated binary example so the dataset
/// loader can be wired against the real JSON. Prints public tournament data only.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Survey of resolved questions in '{tournamentId}' ==");

        // Step 1: collect resolved post ids from the (lightweight) list endpoint.
        var postIds = new List<long>();
        int offset = 0;
        const int page = 50;
        while (true)
        {
            var list = await client.ListPostsAsync(tournamentId, "resolved", page, offset);
            if (!list.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                break;
            foreach (var post in results.EnumerateArray())
                postIds.Add(post.GetProperty("id").GetInt64());
            offset += page;
            if (results.GetArrayLength() < page) break;
        }
        Console.WriteLine($"resolved posts listed: {postIds.Count} (fetching detail for each)");

        // Step 2: resolution + my_forecasts only appear on the detail endpoint.
        int total = 0, withResolution = 0, withMyForecast = 0, scoreable = 0;
        var byType = new Dictionary<string, int>();
        string? binaryExamplePostId = null;
        JsonElement binaryExample = default;

        foreach (long postId in postIds)
        {
            var detail = await client.GetPostAsync(postId);
            if (!detail.TryGetProperty("question", out var q)) continue;
            total++;

            string type = Str(q, "type") ?? "?";
            byType[type] = byType.GetValueOrDefault(type) + 1;

            bool hasResolution = q.TryGetProperty("resolution", out var res) &&
                                 res.ValueKind == JsonValueKind.String &&
                                 !string.IsNullOrEmpty(res.GetString());
            if (hasResolution) withResolution++;

            bool hasMine = q.TryGetProperty("my_forecasts", out var mine) &&
                           mine.TryGetProperty("latest", out var latest) &&
                           latest.ValueKind == JsonValueKind.Object;
            if (hasMine) withMyForecast++;

            if (hasResolution && hasMine) scoreable++;

            if (type == "binary" && binaryExamplePostId is null && hasResolution)
            {
                binaryExamplePostId = postId.ToString();
                binaryExample = q.Clone();
            }
        }

        Console.WriteLine($"total resolved questions : {total}");
        Console.WriteLine($"  with a resolution      : {withResolution}");
        Console.WriteLine($"  with my_forecast.latest: {withMyForecast}");
        Console.WriteLine($"  scoreable (both)       : {scoreable}");
        Console.WriteLine("  by type:");
        foreach (var (t, n) in byType.OrderByDescending(kv => kv.Value))
            Console.WriteLine($"    {t}: {n}");

        if (binaryExamplePostId is not null)
        {
            Console.WriteLine($"\n== Example binary question (post {binaryExamplePostId}) ==");
            Console.WriteLine("resolution: " + Raw(binaryExample, "resolution"));
            Console.WriteLine("\naggregations.unweighted.latest:");
            Console.WriteLine(Truncate(Nested(binaryExample, "aggregations", "unweighted", "latest"), 1500));
            Console.WriteLine("\nmy_forecasts.latest:");
            Console.WriteLine(Truncate(Nested(binaryExample, "my_forecasts", "latest"), 1500));
        }
        else
        {
            Console.WriteLine("\nNo resolved binary question with a resolution found.");
        }
        return 0;
    }

    private static string? Str(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.ToString() : null;

    private static string Raw(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.GetRawText() : "(absent)";

    private static string Nested(JsonElement obj, params string[] path)
    {
        JsonElement cur = obj;
        foreach (var key in path)
        {
            if (cur.ValueKind != JsonValueKind.Object || !cur.TryGetProperty(key, out var next))
                return "(absent)";
            cur = next;
        }
        return cur.GetRawText();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
