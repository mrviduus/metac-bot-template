using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// One resolved binary question the bot forecasted: the realized outcome plus
/// both the bot's and the community's P(Yes) at their latest recorded value.
/// </summary>
public sealed record BinaryRecord(
    long PostId,
    string Title,
    int Outcome,          // 1 = yes, 0 = no
    double BotProbability,
    double? CommunityProbability);

/// <summary>
/// Loads scoreable binary questions from a tournament. Field paths follow the
/// forecasting-tools client: community P(Yes) at aggregations.recency_weighted
/// (fallback unweighted) .latest.centers[0]; the bot's P(Yes) at
/// my_forecasts.latest.forecast_values[1]; resolution at question.resolution.
/// </summary>
public static class Dataset
{
    public static async Task<List<BinaryRecord>> LoadBinaryAsync(
        MetaculusClient client, string tournamentId)
    {
        var records = new List<BinaryRecord>();
        int offset = 0;
        const int page = 60;

        while (true)
        {
            var list = await client.ListPostsAsync(tournamentId, "resolved", page, offset);
            if (!list.TryGetProperty("results", out var results) ||
                results.ValueKind != JsonValueKind.Array || results.GetArrayLength() == 0)
                break;

            foreach (var post in results.EnumerateArray())
            {
                var record = TryParseBinary(post);
                if (record is not null) records.Add(record);
            }

            int returned = results.GetArrayLength();
            offset += returned;
            if (returned < page) break;
        }
        return records;
    }

    private static BinaryRecord? TryParseBinary(JsonElement post)
    {
        if (!post.TryGetProperty("question", out var q)) return null;
        if (GetString(q, "type") != "binary") return null;

        // Outcome: only yes/no are scoreable; annulled/ambiguous are skipped.
        int outcome = GetString(q, "resolution") switch
        {
            "yes" => 1,
            "no" => 0,
            _ => -1,
        };
        if (outcome < 0) return null;

        // Bot forecast: my_forecasts.latest.forecast_values[1] = P(Yes).
        double? botProb = ForecastValueYes(q);
        if (botProb is null) return null;

        double? communityProb = CommunityYes(q);

        return new BinaryRecord(
            PostId: post.GetProperty("id").GetInt64(),
            Title: GetString(post, "title") ?? "",
            Outcome: outcome,
            BotProbability: botProb.Value,
            CommunityProbability: communityProb);
    }

    private static double? ForecastValueYes(JsonElement question)
    {
        if (!question.TryGetProperty("my_forecasts", out var mine)) return null;
        if (!mine.TryGetProperty("latest", out var latest) ||
            latest.ValueKind != JsonValueKind.Object) return null;
        if (!latest.TryGetProperty("forecast_values", out var fv) ||
            fv.ValueKind != JsonValueKind.Array || fv.GetArrayLength() < 2) return null;
        return fv[1].GetDouble();
    }

    private static double? CommunityYes(JsonElement question)
    {
        if (!question.TryGetProperty("aggregations", out var agg)) return null;
        foreach (var method in new[] { "recency_weighted", "unweighted" })
        {
            if (agg.TryGetProperty(method, out var m) &&
                m.TryGetProperty("latest", out var latest) &&
                latest.ValueKind == JsonValueKind.Object &&
                latest.TryGetProperty("centers", out var centers) &&
                centers.ValueKind == JsonValueKind.Array && centers.GetArrayLength() >= 1)
            {
                return centers[0].GetDouble();
            }
        }
        return null;
    }

    private static string? GetString(JsonElement obj, string key) =>
        obj.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;
}
