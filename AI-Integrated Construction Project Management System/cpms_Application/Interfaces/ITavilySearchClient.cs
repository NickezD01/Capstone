namespace cpms_Application.Interfaces
{
    public interface ITavilySearchClient
    {
        Task<TavilySearchResult> SearchAsync(TavilySearchOptions options, CancellationToken cancellationToken = default);
    }

    public class TavilySearchOptions
    {
        public string Query { get; set; } = null!;
        public int MaxResults { get; set; } = 5;
        public string SearchDepth { get; set; } = "basic";
    }

    public class TavilySearchResult
    {
        public bool IsSuccess { get; private set; }
        public string? ErrorMessage { get; private set; }
        public string Query { get; private set; } = string.Empty;
        public IReadOnlyList<TavilySearchItem> Results { get; private set; } = Array.Empty<TavilySearchItem>();

        public static TavilySearchResult Success(string query, IReadOnlyList<TavilySearchItem> results)
        {
            return new TavilySearchResult
            {
                IsSuccess = true,
                Query = query,
                Results = results
            };
        }

        public static TavilySearchResult Failed(string message)
        {
            return new TavilySearchResult
            {
                IsSuccess = false,
                ErrorMessage = message
            };
        }

        public string ToContextBlock()
        {
            if (Results.Count == 0)
                return "No web search results were found.";

            var lines = new List<string> { "Web search results:" };
            for (var i = 0; i < Results.Count; i++)
            {
                var item = Results[i];
                lines.Add($"{i + 1}. {item.Title}");
                lines.Add($"   URL: {item.Url}");
                if (!string.IsNullOrWhiteSpace(item.Content))
                    lines.Add($"   Snippet: {item.Content.Trim()}");
            }

            return string.Join(Environment.NewLine, lines);
        }
    }

    public class TavilySearchItem
    {
        public string Title { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public double Score { get; set; }
    }
}
