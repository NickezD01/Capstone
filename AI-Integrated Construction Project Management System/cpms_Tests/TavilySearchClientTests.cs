using cpms_Application.Services;
using cpms_Domain;
using System.Net;
using System.Text;

namespace cpms_Tests;

public class TavilySearchClientTests
{
    [Fact]
    public async Task SearchAsyncSendsBearerAuthorizationAndQuery()
    {
        var handler = new CaptureRequestHandler();
        var client = new TavilySearchClient(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.tavily.com/")
        }, new AppSetting
        {
            Tavily = new Tavily
            {
                ApiKey = "tvly-test-key",
                DefaultMaxResults = 5,
                SearchDepth = "basic"
            }
        });

        var result = await client.SearchAsync(new cpms_Application.Interfaces.TavilySearchOptions
        {
            Query = "construction suppliers in Hanoi",
            MaxResults = 3
        });

        Assert.True(result.IsSuccess);
        Assert.Single(result.Results);
        Assert.Equal("Example Supplier", result.Results[0].Title);
        Assert.NotNull(handler.RequestBody);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("tvly-test-key", handler.AuthorizationParameter);
        Assert.Contains("construction suppliers in Hanoi", handler.RequestBody);
    }

    private sealed class CaptureRequestHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public string? AuthorizationScheme { get; private set; }
        public string? AuthorizationParameter { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "query": "construction suppliers in Hanoi",
                      "results": [
                        {
                          "title": "Example Supplier",
                          "url": "https://example.com",
                          "content": "Supplier details",
                          "score": 0.91
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        }
    }
}
