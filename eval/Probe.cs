using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper. For the given tournament ("public" = no tournament filter),
/// scans resolved binary questions and reports how many expose a yes/no resolution
/// and a community aggregation. Distinguishes "AIB questions are redacted for this
/// token" from "our request is wrong" by comparing AIB vs general public questions.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Resolution/CP availability in '{tournamentId}' ==");

        var list = await client.ListPostsAsync(tournamentId, "resolved", limit: 40);
        if (!list.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array)
        {
            Console.WriteLine("No results.");
            return 0;
        }

        int binary = 0, withRes = 0, withCp = 0;
        int shown = 0;
        foreach (var post in results.EnumerateArray())
        {
            if (!post.TryGetProperty("question", out var q)) continue;
            if (GetString(q, "type") != "binary") continue;
            binary++;

            string? res = GetString(q, "resolution");
            bool hasRes = !string.IsNullOrEmpty(res);
            bool hasCp = HasCommunity(q);
            if (hasRes) withRes++;
            if (hasCp) withCp++;

            if (shown++ < 6)
            {
                Console.WriteLine($"  #{post.GetProperty("id").GetInt64()} " +
                                  $"res={res ?? "null",-6} cp={(hasCp ? "yes" : "no ")} " +
                                  $"{Truncate(GetString(post, "title") ?? "", 55)}");
            }
        }

        Console.WriteLine($"\nbinary={binary} withResolution={withRes} withCommunity={withCp}");
        return 0;
    }

    /// <summary>Fetch one post by id and report whether content/CP fields are populated.</summary>
    public static async Task<int> InspectOneAsync(string postIdArg)
    {
        using var client = new MetaculusClient();
        long postId = long.Parse(postIdArg);
        Console.WriteLine($"== Inspecting post {postId} (detail+with_cp) ==");
        var detail = await client.GetPostAsync(postId);
        Console.WriteLine("post title      : " + GetString(detail, "title"));
        if (!detail.TryGetProperty("question", out var q))
        {
            Console.WriteLine("no question (maybe group/multiple). top-level keys:");
            foreach (var p in detail.EnumerateObject()) Console.WriteLine("  " + p.Name);
            return 0;
        }
        Console.WriteLine("type            : " + GetString(q, "type"));
        Console.WriteLine("resolution      : " + Raw(q, "resolution"));
        Console.WriteLine("description!=null: " + (GetString(q, "description") is { Length: > 0 }));
        Console.WriteLine("res_criteria!=null: " + (GetString(q, "resolution_criteria") is { Length: > 0 }));
        Console.WriteLine("has community   : " + HasCommunity(q));
        Console.WriteLine("\naggregations (truncated):");
        Console.WriteLine(Truncate(Raw(q, "aggregations"), 1200));
        return 0;
    }

    private static string Raw(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.GetRawText() : "(absent)";

    private static bool HasCommunity(JsonElement question)
    {
        if (!question.TryGetProperty("aggregations", out var agg)) return false;
        foreach (var method in new[] { "recency_weighted", "unweighted" })
        {
            if (agg.TryGetProperty(method, out var m) &&
                m.TryGetProperty("latest", out var latest) &&
                latest.ValueKind == JsonValueKind.Object &&
                latest.TryGetProperty("centers", out var c) &&
                c.ValueKind == JsonValueKind.Array && c.GetArrayLength() >= 1)
                return true;
        }
        return false;
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
