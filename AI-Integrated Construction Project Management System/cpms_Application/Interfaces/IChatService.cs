using cpms_Application.Request.Chat;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IChatService
    {
        Task<ApiResponse> CreateConversationAsync(CreateConversationRequest request);
        Task<ApiResponse> GetProjectConversationsAsync(int projectId);
        Task<ApiResponse> GetMessagesAsync(int conversationId);
        Task<ApiResponse> SendMessageAsync(int conversationId, SendMessageRequest request);
        Task<ApiResponse> UpdateMessageAsync(int messageId, UpdateMessageRequest request);
        Task<ApiResponse> DeleteMessageAsync(int messageId);
        Task<ApiResponse> MarkConversationReadAsync(int conversationId);
    }
}
