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

    /// <summary>Fetch one page of posts for a tournament with a given status.</summary>
    public async Task<JsonElement> ListPostsAsync(
        string tournamentId, string status = "resolved", int limit = 20, int offset = 0)
    {
        string url = $"{BaseUrl}/posts/?tournaments={tournamentId}" +
                     $"&statuses={status}&limit={limit}&offset={offset}&include_description=true";
        return await GetJsonAsync(url);
    }

    /// <summary>Fetch full detail for a single post (includes question + forecasts).</summary>
    public async Task<JsonElement> GetPostAsync(long postId)
    {
        return await GetJsonAsync($"{BaseUrl}/posts/{postId}/");
    }

    private async Task<JsonElement> GetJsonAsync(string url)
    {
        using var resp = await _http.GetAsync(url);
        string body = await resp.Content.ReadAsStringAsync();
        if (!resp.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"GET {url} -> {(int)resp.StatusCode}: {Truncate(body, 300)}");
        }
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.Clone();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    public void Dispose() => _http.Dispose();
}
