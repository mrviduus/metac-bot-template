using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Development helper: dumps the shape of the Metaculus API responses so the
/// dataset loader can be wired against the real JSON. Prints tournament data
/// (public, non-secret); never prints the auth token.
/// </summary>
internal static class Probe
{
    public static async Task<int> RunAsync(string tournamentId)
    {
        using var client = new MetaculusClient();

        Console.WriteLine($"== Listing resolved posts for tournament '{tournamentId}' ==");
        var list = await client.ListPostsAsync(tournamentId, status: "resolved", limit: 5);

        if (list.TryGetProperty("count", out var count))
            Console.WriteLine($"count = {count}");

        if (!list.TryGetProperty("results", out var results) ||
            results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
        {
            Console.WriteLine("No resolved posts returned.");
            return 0;
        }

        Console.WriteLine("\n-- top-level keys of first result --");
        var first = results[0];
        PrintKeys(first, indent: 1);

        if (first.TryGetProperty("question", out var q))
        {
            Console.WriteLine("\n-- keys of results[0].question --");
            PrintKeys(q, indent: 1);
        }

        long postId = first.GetProperty("id").GetInt64();
        Console.WriteLine($"\n== Full detail for post {postId} ==");
        var detail = await client.GetPostAsync(postId);
        Console.WriteLine("\n-- top-level keys of post detail --");
        PrintKeys(detail, indent: 1);

        if (detail.TryGetProperty("question", out var dq))
        {
            Console.WriteLine("\n-- keys of detail.question --");
            PrintKeys(dq, indent: 1);
            Console.WriteLine("\n-- raw detail.question (truncated) --");
            Console.WriteLine(Truncate(dq.GetRawText(), 4000));
        }

        return 0;
    }

    private static void PrintKeys(JsonElement obj, int indent)
    {
        if (obj.ValueKind != JsonValueKind.Object) return;
        string pad = new(' ', indent * 2);
        foreach (var prop in obj.EnumerateObject())
        {
            string kind = prop.Value.ValueKind switch
            {
                JsonValueKind.Object => "object",
                JsonValueKind.Array => $"array[{prop.Value.GetArrayLength()}]",
                JsonValueKind.String => $"string \"{Truncate(prop.Value.GetString() ?? "", 40)}\"",
                JsonValueKind.Number => $"number {prop.Value.GetRawText()}",
                _ => prop.Value.ValueKind.ToString().ToLowerInvariant(),
            };
            Console.WriteLine($"{pad}{prop.Name}: {kind}");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";
}
