using cpms_Application.Request.AiChat;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IAiChatService
    {
        Task<ApiResponse> CreateSessionAsync(CreateAiChatSessionRequest request);
        Task<ApiResponse> GetSessionsAsync();
        Task<ApiResponse> GetMessagesAsync(int sessionId);
        Task<ApiResponse> SendMessageAsync(int sessionId, SendAiChatMessageRequest request);
        Task<ApiResponse> DeleteSessionAsync(int sessionId);
    }
}
