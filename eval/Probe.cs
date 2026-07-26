using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper: dumps the raw resolution / aggregations / my_forecasts of
/// one resolved binary question straight from the list+with_cp response, so we
/// can see exactly which fields the API populates. One request.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();
        Console.WriteLine($"== Raw fields of a resolved binary in '{tournamentId}' (list+with_cp) ==");

        var list = await client.ListPostsAsync(tournamentId, "resolved", limit: 60);
        if (!list.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            Console.WriteLine("No results.");
            return 0;
        }

        foreach (var post in results.EnumerateArray())
        {
            if (!post.TryGetProperty("question", out var q)) continue;
            if (q.TryGetProperty("type", out var t) && t.GetString() == "binary")
            {
                long id = post.GetProperty("id").GetInt64();
                Console.WriteLine($"post {id}: {GetString(post, "title")}\n");
                Console.WriteLine("resolution         : " + Raw(q, "resolution"));
                Console.WriteLine("actual_resolve_time: " + Raw(q, "actual_resolve_time"));
                Console.WriteLine("open_time          : " + Raw(q, "open_time"));
                Console.WriteLine("\naggregations:");
                Console.WriteLine(Truncate(Raw(q, "aggregations"), 1800));
                Console.WriteLine("\nmy_forecasts:");
                Console.WriteLine(Truncate(Raw(q, "my_forecasts"), 1200));
                return 0;
            }
        }
        Console.WriteLine("No binary question in the first page.");
        return 0;
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.ToString() : null;

    private static string Raw(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) ? v.GetRawText() : "(absent)";

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
