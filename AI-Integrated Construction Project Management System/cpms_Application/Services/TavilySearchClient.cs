using cpms_Application.Interfaces;
using cpms_Domain;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace cpms_Application.Services
{
    public class TavilySearchClient : ITavilySearchClient
    {
        private readonly HttpClient _httpClient;
        private readonly AppSetting _appSetting;

        public TavilySearchClient(HttpClient httpClient, AppSetting appSetting)
        {
            _httpClient = httpClient;
            _appSetting = appSetting;
        }

        public async Task<TavilySearchResult> SearchAsync(TavilySearchOptions options, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(options.Query))
                return TavilySearchResult.Failed("Search query is required.");

            var tavily = _appSetting.Tavily;
            if (string.IsNullOrWhiteSpace(tavily.ApiKey))
                return TavilySearchResult.Failed("Tavily:ApiKey is not configured.");

            var payload = new
            {
                query = options.Query.Trim(),
                search_depth = string.IsNullOrWhiteSpace(options.SearchDepth) ? "basic" : options.SearchDepth,
                max_results = Math.Clamp(options.MaxResults, 1, 20),
                include_answer = false
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "search");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tavily.ApiKey);
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    return TavilySearchResult.Failed($"Tavily returned {(int)response.StatusCode}: {responseText}");

                var parsed = JsonSerializer.Deserialize<TavilySearchResponse>(responseText, JsonOptions);
                if (parsed?.Results == null || parsed.Results.Count == 0)
                    return TavilySearchResult.Success(options.Query.Trim(), Array.Empty<TavilySearchItem>());

                var items = parsed.Results
                    .Select(r => new TavilySearchItem
                    {
                        Title = r.Title ?? string.Empty,
                        Url = r.Url ?? string.Empty,
                        Content = r.Content ?? string.Empty,
                        Score = r.Score ?? 0
                    })
                    .ToList();

                return TavilySearchResult.Success(parsed.Query ?? options.Query.Trim(), items);
            }
            catch (Exception ex)
            {
                return TavilySearchResult.Failed("Tavily search request failed: " + ex.Message);
            }
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private sealed class TavilySearchResponse
        {
            public string? Query { get; set; }

            [JsonPropertyName("results")]
            public List<TavilySearchResponseItem>? Results { get; set; }
        }

        private sealed class TavilySearchResponseItem
        {
            public string? Title { get; set; }
            public string? Url { get; set; }
            public string? Content { get; set; }
            public double? Score { get; set; }
        }
    }
}
