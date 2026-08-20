using cpms_Application.Interfaces;
using cpms_Application.Request.AiChat;
using cpms_Application.Response;
using cpms_Application.Response.AiChat;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace cpms_Application.Services
{
    public class AiChatService : IAiChatService
    {
        private const int MaxHistoryMessages = 20;
        private const string DefaultSystemInstruction =
            "You are BuildSense AI Assistant, a helpful assistant for construction project management. " +
            "Help users with project planning, tasks, materials, suppliers, warehouses, budgets, and general construction management questions. " +
            "Be concise, practical, and professional. If you are unsure, say so instead of guessing.";

        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;
        private readonly IGoogleAIClient _googleAIClient;

        public AiChatService(IUnitOfWork uow, IClaimService claimService, IGoogleAIClient googleAIClient)
        {
            _uow = uow;
            _claimService = claimService;
            _googleAIClient = googleAIClient;
        }

        public async Task<ApiResponse> CreateSessionAsync(CreateAiChatSessionRequest request)
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();

            if (request.ProjectId.HasValue)
            {
                var project = await _uow.Projects.GetByIdAsync(request.ProjectId.Value);
                if (project == null)
                    return response.SetNotFound("Project not found.");
            }

            var title = string.IsNullOrWhiteSpace(request.Title) ? "New chat" : request.Title.Trim();
            var session = new AiChatSession
            {
                UserId = currentUser.Id,
                ProjectId = request.ProjectId,
                Title = title.Length > 200 ? title[..200] : title,
                LastMessageAt = DateTime.UtcNow
            };

            await _uow.AiChatSessions.AddAsync(session);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapSession(session, 0));
        }

        public async Task<ApiResponse> GetSessionsAsync()
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();

            var sessions = await _uow.AiChatSessions.GetAllAsync(
                filter: s => s.UserId == currentUser.Id && !s.IsDeleted,
                include: query => query.Include(s => s.Messages)
            );

            return response.SetOk(sessions
                .OrderByDescending(s => s.LastMessageAt)
                .Select(s => MapSession(s, s.Messages.Count(m => !m.IsDeleted)))
                .ToList());
        }

        public async Task<ApiResponse> GetMessagesAsync(int sessionId)
        {
            var response = new ApiResponse();
            var session = await GetOwnedSessionAsync(sessionId);
            if (session == null)
                return response.SetNotFound("Chat session not found.");

            var messages = await _uow.AiChatMessages.GetAllAsync(
                filter: m => m.SessionId == sessionId && !m.IsDeleted
            );

            return response.SetOk(messages
                .OrderBy(m => m.SentAt)
                .Select(MapMessage)
                .ToList());
        }

        public async Task<ApiResponse> SendMessageAsync(int sessionId, SendAiChatMessageRequest request)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(request.Message))
                return response.SetBadRequest("Message is required.");

            var session = await GetOwnedSessionAsync(sessionId, includeMessages: true);
            if (session == null)
                return response.SetNotFound("Chat session not found.");

            var userContent = request.Message.Trim();
            var userMessage = new AiChatMessage
            {
                SessionId = sessionId,
                Role = AiChatRole.User,
                Content = userContent,
                SentAt = DateTime.UtcNow
            };

            await _uow.AiChatMessages.AddAsync(userMessage);

            var history = session.Messages
                .Where(m => !m.IsDeleted)
                .OrderByDescending(m => m.SentAt)
                .Take(MaxHistoryMessages)
                .OrderBy(m => m.SentAt)
                .ToList();

            history.Add(userMessage);

            var systemInstruction = await BuildSystemInstructionAsync(session);
            var prompt = BuildConversationPrompt(history);

            var aiResult = request.UseWebSearch
                ? await _googleAIClient.GenerateGroundedTextAsync(systemInstruction, prompt)
                : await _googleAIClient.GenerateTextAsync(systemInstruction, prompt);

            if (!aiResult.IsSuccess)
                return response.SetBadRequest(aiResult.ErrorMessage ?? "AI request failed.");

            var assistantMessage = new AiChatMessage
            {
                SessionId = sessionId,
                Role = AiChatRole.Assistant,
                Content = aiResult.Text!.Trim(),
                SentAt = DateTime.UtcNow
            };

            await _uow.AiChatMessages.AddAsync(assistantMessage);

            session.LastMessageAt = assistantMessage.SentAt;
            if (session.Title == "New chat" && userContent.Length > 0)
            {
                session.Title = userContent.Length > 60 ? userContent[..60] + "..." : userContent;
            }

            _uow.AiChatSessions.Update(session);
            await _uow.SaveChangeAsync();

            return response.SetOk(new AiChatReplyResponse
            {
                UserMessage = MapMessage(userMessage),
                AssistantMessage = MapMessage(assistantMessage)
            });
        }

        public async Task<ApiResponse> DeleteSessionAsync(int sessionId)
        {
            var response = new ApiResponse();
            var session = await GetOwnedSessionAsync(sessionId);
            if (session == null)
                return response.SetNotFound("Chat session not found.");

            session.IsDeleted = true;
            session.ModifiedDate = DateTime.UtcNow;
            _uow.AiChatSessions.Update(session);
            await _uow.SaveChangeAsync();

            return response.SetOk("Chat session deleted.");
        }

        private async Task<AiChatSession?> GetOwnedSessionAsync(int sessionId, bool includeMessages = false)
        {
            var currentUser = _claimService.GetUserClaim();
            return await _uow.AiChatSessions.GetAsync(
                s => s.SessionId == sessionId && s.UserId == currentUser.Id && !s.IsDeleted,
                include: includeMessages
                    ? query => query.Include(s => s.Messages)
                    : null
            );
        }

        private async Task<string> BuildSystemInstructionAsync(AiChatSession session)
        {
            if (!session.ProjectId.HasValue)
                return DefaultSystemInstruction;

            var project = await _uow.Projects.GetByIdAsync(session.ProjectId.Value);
            if (project == null)
                return DefaultSystemInstruction;

            return DefaultSystemInstruction +
                   $" The user is asking about project \"{project.ProjectName}\" (ID: {project.ProjectId}). " +
                   "Use this project context when answering, but do not invent data that was not provided.";
        }

        private static string BuildConversationPrompt(IEnumerable<AiChatMessage> messages)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Conversation history:");
            foreach (var message in messages)
            {
                var speaker = message.Role == AiChatRole.User ? "User" : "Assistant";
                builder.AppendLine($"{speaker}: {message.Content}");
            }

            builder.AppendLine();
            builder.AppendLine("Reply to the latest user message as the Assistant.");
            return builder.ToString();
        }

        private static AiChatSessionResponse MapSession(AiChatSession session, int messageCount)
        {
            return new AiChatSessionResponse
            {
                SessionId = session.SessionId,
                UserId = session.UserId,
                ProjectId = session.ProjectId,
                Title = session.Title,
                CreatedAt = session.CreatedDate ?? session.LastMessageAt,
                LastMessageAt = messageCount > 0 ? session.LastMessageAt : null,
                MessageCount = messageCount
            };
        }

        private static AiChatMessageResponse MapMessage(AiChatMessage message)
        {
            return new AiChatMessageResponse
            {
                MessageId = message.MessageId,
                SessionId = message.SessionId,
                Role = message.Role,
                Content = message.Content,
                CreatedAt = message.CreatedDate ?? message.SentAt,
                SentAt = message.SentAt
            };
        }
    }
}
