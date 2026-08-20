using cpms_Application.Services;
using cpms_Domain;
using System.Net;
using System.Text;
using System.Text.Json;

namespace cpms_Tests;

public class GoogleAIClientTests
{
    [Fact]
    public async Task GenerateTextAsyncDoesNotSendSearchTools()
    {
        var handler = new CaptureRequestHandler();
        var client = new GoogleAIClient(new HttpClient(handler), new AppSetting
        {
            GoogleAI = new GoogleAI
            {
                ApiKey = "test-api-key",
                Model = "gemini-test-model"
            }
        });

        var result = await client.GenerateTextAsync("system", "input");

        Assert.True(result.IsSuccess);
        Assert.Equal("AI reply", result.Text);
        Assert.NotNull(handler.RequestBody);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        Assert.False(document.RootElement.TryGetProperty("tools", out _));
    }

    [Fact]
    public async Task GenerateTextAsyncMaps429WithoutExposingGooglePayload()
    {
        var handler = new CaptureRequestHandler
        {
            StatusCode = HttpStatusCode.TooManyRequests,
            ResponseBody = """{"error":{"message":"You exceeded your current quota","code":"too_many_requests"}}"""
        };
        var client = new GoogleAIClient(new HttpClient(handler), new AppSetting
        {
            GoogleAI = new GoogleAI { ApiKey = "test-api-key" }
        });

        var result = await client.GenerateTextAsync("system", "input");

        Assert.False(result.IsSuccess);
        Assert.True(result.IsRateLimited);
        Assert.Equal(HttpStatusCode.TooManyRequests, result.StatusCode);
        Assert.DoesNotContain("too_many_requests", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("rate limit", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class CaptureRequestHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }
        public HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        public string ResponseBody { get; set; } = """{"output_text":"AI reply"}""";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(StatusCode)
            {
                Content = new StringContent(ResponseBody, Encoding.UTF8, "application/json")
            };
        }
    }
}
