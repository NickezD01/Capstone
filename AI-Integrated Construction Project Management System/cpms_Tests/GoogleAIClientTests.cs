using cpms_Application.Services;
using cpms_Domain;
using System.Net;
using System.Text;
using System.Text.Json;

namespace cpms_Tests;

public class GoogleAIClientTests
{
    [Fact]
    public async Task GenerateGroundedTextAsyncSendsGoogleSearchToolType()
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

        var result = await client.GenerateGroundedTextAsync("system", "input");

        Assert.True(result.IsSuccess);
        Assert.Equal("AI reply", result.Text);
        Assert.NotNull(handler.RequestBody);

        using var document = JsonDocument.Parse(handler.RequestBody!);
        var tool = document.RootElement.GetProperty("tools")[0];
        Assert.Equal("google_search", tool.GetProperty("type").GetString());
        Assert.False(tool.TryGetProperty("google_search", out _));
    }

    private sealed class CaptureRequestHandler : HttpMessageHandler
    {
        public string? RequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestBody = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"output_text":"AI reply"}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
