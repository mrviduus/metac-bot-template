using System.Net.Http.Headers;
using System.Text.Json;

namespace PredictKitEval;

/// <summary>
/// Thin read-only client for the Metaculus API. Auth token is read from the
/// METACULUS_TOKEN environment variable (the same secret the live bot uses),
/// so nothing sensitive is ever passed on the command line or logged.
/// </summary>
public sealed class MetaculusClient : IDisposable
{
    private const string BaseUrl = "https://www.metaculus.com/api";
    private readonly HttpClient _http = new();

    public MetaculusClient()
    {
        string? token = Environment.GetEnvironmentVariable("METACULUS_TOKEN");
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "METACULUS_TOKEN is not set. In CI it comes from repo secrets; " +
                "locally, export it before running.");
        }
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Token", token);
    }

    /// <summary>
    /// Fetch one page of posts for a tournament. with_cp=true is required for the
    /// response to include community aggregations, resolution, and my_forecasts —
    /// without it those fields come back null.
    /// </summary>
    public async Task<JsonElement> ListPostsAsync(
        string tournamentId, string status = "resolved", int limit = 20, int offset = 0)
    {
        string url = $"{BaseUrl}/posts/?tournaments={tournamentId}" +
                     $"&statuses={status}&limit={limit}&offset={offset}&with_cp=true";
        return await GetJsonAsync(url);
    }

    /// <summary>Fetch full detail for a single post (includes question + forecasts).</summary>
    public async Task<JsonElement> GetPostAsync(long postId)
    {
        return await GetJsonAsync($"{BaseUrl}/posts/{postId}/?with_cp=true");
    }

    // Metaculus sits behind Cloudflare, which returns 429 (error 1015) on bursts.
    // A politeness delay between calls plus backoff on 429/5xx keeps a full-
    // tournament scan (hundreds of detail calls) under the limit.
    private static readonly TimeSpan MinInterval = TimeSpan.FromMilliseconds(1500);
    private const int MaxRetries = 6;
    private DateTime _lastRequestUtc = DateTime.MinValue;

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        for (int attempt = 0; ; attempt++)
        {
            await ThrottleAsync();
            using var resp = await _http.GetAsync(url);
            string body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                return doc.RootElement.Clone();
            }

            bool retryable = (int)resp.StatusCode == 429 || (int)resp.StatusCode >= 500;
            if (!retryable || attempt >= MaxRetries)
            {
                throw new HttpRequestException(
                    $"GET {url} -> {(int)resp.StatusCode}: {Truncate(body, 300)}");
            }

            // Exponential floor 3,6,12,24s; take the larger of that and any
            // (sane) Retry-After the server sends. Cloudflare's Retry-After is
            // sometimes 0/absent, so it can only extend the wait, never shorten it.
            TimeSpan floor = TimeSpan.FromSeconds(3 * Math.Pow(2, attempt));
            TimeSpan hinted = resp.Headers.RetryAfter?.Delta ?? TimeSpan.Zero;
            TimeSpan wait = hinted > floor ? hinted : floor;
            Console.WriteLine($"  {(int)resp.StatusCode} on {url} — retry in {wait.TotalSeconds:F0}s (attempt {attempt + 1})");
            await Task.Delay(wait);
        }
    }

    private async Task ThrottleAsync()
    {
        TimeSpan since = DateTime.UtcNow - _lastRequestUtc;
        if (since < MinInterval)
            await Task.Delay(MinInterval - since);
        _lastRequestUtc = DateTime.UtcNow;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    public void Dispose() => _http.Dispose();
}
