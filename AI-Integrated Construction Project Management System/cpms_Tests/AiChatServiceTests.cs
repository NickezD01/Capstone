using cpms_Application.Interfaces;
using cpms_Application.Request.AiChat;
using cpms_Application.Response.AiChat;
using cpms_Application.Services;
using cpms_Domain;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Tests;

public class AiChatServiceTests
{
    [Fact]
    public async Task CreateSessionAllowsEmptyOptionalFields()
    {
        var uow = new TestUnitOfWork();
        var service = CreateService(uow);

        var response = await service.CreateSessionAsync(new CreateAiChatSessionRequest());

        Assert.True(response.IsSuccess);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var session = Assert.IsType<AiChatSessionResponse>(response.Result);
        Assert.Equal(1, session.SessionId);
        Assert.Equal(7, session.UserId);
        Assert.Equal("New chat", session.Title);
        Assert.Null(session.ProjectId);
        Assert.NotNull(session.CreatedAt);
        Assert.Null(session.LastMessageAt);
        Assert.Equal(0, session.MessageCount);
    }

    [Fact]
    public async Task SendMessageWithWebSearchCallsTavilyThenGeminiReasoning()
    {
        var uow = SeedSession();
        var google = new FakeGoogleAIClient();
        var tavily = new FakeTavilySearchClient();
        var service = CreateService(uow, google, tavily);

        var response = await service.SendMessageAsync(1, new SendAiChatMessageRequest
        {
            Message = "Current cement prices in Hanoi?",
            UseWebSearch = true
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(1, tavily.CallCount);
        Assert.Equal("Current cement prices in Hanoi?", tavily.LastQuery);
        Assert.Equal(1, google.CallCount);
        Assert.Contains("Web search results:", google.LastInput);
        Assert.Contains("https://example.com/cement", google.LastInput);
        Assert.DoesNotContain("google_search", google.LastInput);

        var reply = Assert.IsType<AiChatReplyResponse>(response.Result);
        Assert.True(reply.UsedWebSearch);
        Assert.Single(reply.WebSearchSources);
        Assert.Equal("https://example.com/cement", reply.WebSearchSources[0].Url);
    }

    [Fact]
    public async Task SendMessageWithoutWebSearchDoesNotCallTavily()
    {
        var uow = SeedSession();
        var google = new FakeGoogleAIClient();
        var tavily = new FakeTavilySearchClient();
        var service = CreateService(uow, google, tavily);

        var response = await service.SendMessageAsync(1, new SendAiChatMessageRequest
        {
            Message = "Summarize this project.",
            UseWebSearch = false
        });

        Assert.True(response.IsSuccess);
        Assert.Equal(0, tavily.CallCount);
        Assert.Equal(1, google.CallCount);
        Assert.DoesNotContain("Web search results:", google.LastInput);

        var reply = Assert.IsType<AiChatReplyResponse>(response.Result);
        Assert.False(reply.UsedWebSearch);
        Assert.Empty(reply.WebSearchSources);
    }

    [Fact]
    public async Task SendMessageWithWebSearchReturnsGeminiRateLimitAfterTavilySucceeds()
    {
        var uow = SeedSession();
        var google = new FakeGoogleAIClient
        {
            NextResult = GoogleAITextResult.Failed("Gemini rate limit exceeded.", HttpStatusCode.TooManyRequests)
        };
        var tavily = new FakeTavilySearchClient();
        var service = CreateService(uow, google, tavily);

        var response = await service.SendMessageAsync(1, new SendAiChatMessageRequest
        {
            Message = "Current cement prices in Hanoi?",
            UseWebSearch = true
        });

        Assert.False(response.IsSuccess);
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        Assert.Equal(1, tavily.CallCount);
        Assert.Equal(1, google.CallCount);
        Assert.Contains("Tavily web search succeeded", response.ErrorMessage);
        Assert.DoesNotContain("too_many_requests", response.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    private static TestUnitOfWork SeedSession()
    {
        var uow = new TestUnitOfWork();
        uow.AiChatSessionRecords.Add(new AiChatSession
        {
            SessionId = 1,
            UserId = 7,
            Title = "New chat",
            LastMessageAt = DateTime.UtcNow
        });
        return uow;
    }

    private static AiChatService CreateService(
        TestUnitOfWork uow,
        IGoogleAIClient? google = null,
        ITavilySearchClient? tavily = null) =>
        new(
            uow,
            new FakeClaimService(7, Role.PM),
            google ?? new FakeGoogleAIClient(),
            tavily ?? new FakeTavilySearchClient(),
            new AppSetting());
}

internal sealed class FakeGoogleAIClient : IGoogleAIClient
{
    public int CallCount { get; private set; }
    public string? LastInput { get; private set; }
    public GoogleAITextResult NextResult { get; set; } = GoogleAITextResult.Success("Test reply");

    public Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input)
    {
        CallCount++;
        LastInput = input;
        return Task.FromResult(NextResult);
    }
}

internal sealed class FakeTavilySearchClient : ITavilySearchClient
{
    public int CallCount { get; private set; }
    public string? LastQuery { get; private set; }

    public Task<TavilySearchResult> SearchAsync(TavilySearchOptions options, CancellationToken cancellationToken = default)
    {
        CallCount++;
        LastQuery = options.Query;
        return Task.FromResult(TavilySearchResult.Success(options.Query, new[]
        {
            new TavilySearchItem
            {
                Title = "Cement prices",
                Url = "https://example.com/cement",
                Content = "Current cement prices in Hanoi are..."
            }
        }));
    }
}
