using cpms_Application.Interfaces;
using cpms_Application.Request.AiChat;
using cpms_Application.Response.AiChat;
using cpms_Application.Services;
using cpms_Domain.Models;
using System.Net;

namespace cpms_Tests;

public class AiChatServiceTests
{
    [Fact]
    public async Task CreateSessionAllowsEmptyOptionalFields()
    {
        var uow = new TestUnitOfWork();
        var service = new AiChatService(uow, new FakeClaimService(7, Role.PM), new FakeGoogleAIClient());

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
}

internal sealed class FakeGoogleAIClient : IGoogleAIClient
{
    public Task<GoogleAITextResult> GenerateTextAsync(string systemInstruction, string input) =>
        Task.FromResult(GoogleAITextResult.Success("Test reply"));

    public Task<GoogleAITextResult> GenerateGroundedTextAsync(string systemInstruction, string input) =>
        Task.FromResult(GoogleAITextResult.Success("Grounded test reply"));
}
