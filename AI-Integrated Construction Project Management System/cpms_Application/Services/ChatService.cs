using cpms_Application.Interfaces;
using cpms_Application.Request.Chat;
using cpms_Application.Response;
using cpms_Application.Response.Chat;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace cpms_Application.Services
{
    public class ChatService : IChatService
    {
        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;

        public ChatService(IUnitOfWork uow, IClaimService claimService)
        {
            _uow = uow;
            _claimService = claimService;
        }

        public async Task<ApiResponse> CreateConversationAsync(CreateConversationRequest request)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(request.Title))
                return response.SetBadRequest("Conversation title is required.");

            var currentUser = _claimService.GetUserClaim();
            var project = await _uow.Projects.GetByIdAsync(request.ProjectId);
            if (project == null)
                return response.SetNotFound("Project not found.");

            if (request.TaskId.HasValue)
            {
                var task = await _uow.TaskItems.GetAsync(t => t.TaskId == request.TaskId.Value && t.ProjectId == request.ProjectId);
                if (task == null)
                    return response.SetBadRequest("Task does not belong to this project.");
            }

            var participantIds = request.ParticipantUserIds
                .Append(currentUser.Id)
                .Distinct()
                .ToList();

            foreach (var userId in participantIds)
            {
                var user = await _uow.UserAccounts.GetByIdAsync(userId);
                if (user == null)
                    return response.SetBadRequest($"UserId {userId} does not exist.");
            }

            var conversation = new ChatConversation
            {
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                Title = request.Title.Trim(),
                Type = request.Type,
                LastMessageAt = DateTime.UtcNow
            };

            foreach (var userId in participantIds)
            {
                conversation.Participants.Add(new ChatParticipant
                {
                    UserId = userId,
                    JoinedAt = DateTime.UtcNow
                });
            }

            await _uow.ChatConversations.AddAsync(conversation);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapConversation(conversation));
        }

        public async Task<ApiResponse> GetProjectConversationsAsync(int projectId)
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();

            var conversations = await _uow.ChatConversations.GetAllAsync(
                filter: c => c.ProjectId == projectId && c.Participants.Any(p => p.UserId == currentUser.Id),
                include: query => query
                    .Include(c => c.Participants)
                    .ThenInclude(p => p.User)
            );

            return response.SetOk(conversations
                .OrderByDescending(c => c.LastMessageAt)
                .Select(MapConversation)
                .ToList());
        }

        public async Task<ApiResponse> GetMessagesAsync(int conversationId)
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();
            if (!await IsParticipantAsync(conversationId, currentUser.Id))
                return response.SetBadRequest("You are not a participant in this conversation.");

            var messages = await _uow.ChatMessages.GetAllAsync(
                filter: m => m.ConversationId == conversationId,
                include: query => query.Include(m => m.Sender)
            );

            return response.SetOk(messages
                .OrderBy(m => m.SentAt)
                .Select(MapMessage)
                .ToList());
        }

        public async Task<ApiResponse> SendMessageAsync(int conversationId, SendMessageRequest request)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(request.Body) && string.IsNullOrWhiteSpace(request.AttachmentUrl))
                return response.SetBadRequest("Message body or attachment is required.");

            var currentUser = _claimService.GetUserClaim();
            var conversation = await _uow.ChatConversations.GetAsync(
                c => c.ConversationId == conversationId,
                include: query => query.Include(c => c.Participants)
            );

            if (conversation == null)
                return response.SetNotFound("Conversation not found.");

            if (!conversation.Participants.Any(p => p.UserId == currentUser.Id))
                return response.SetBadRequest("You are not a participant in this conversation.");

            var message = new ChatMessage
            {
                ConversationId = conversationId,
                SenderId = currentUser.Id,
                Body = request.Body?.Trim() ?? string.Empty,
                AttachmentUrl = request.AttachmentUrl,
                SentAt = DateTime.UtcNow
            };

            conversation.LastMessageAt = message.SentAt;
            _uow.ChatConversations.Update(conversation);
            await _uow.ChatMessages.AddAsync(message);
            await _uow.SaveChangeAsync();

            message.Sender = await _uow.UserAccounts.GetByIdAsync(currentUser.Id) ?? message.Sender;
            return response.SetOk(MapMessage(message));
        }

        public async Task<ApiResponse> UpdateMessageAsync(int messageId, UpdateMessageRequest request)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(request.Body))
                return response.SetBadRequest("Message body is required.");

            var currentUser = _claimService.GetUserClaim();
            var message = await _uow.ChatMessages.GetAsync(m => m.MessageId == messageId, query => query.Include(m => m.Sender));
            if (message == null)
                return response.SetNotFound("Message not found.");

            if (message.SenderId != currentUser.Id)
                return response.SetBadRequest("Only the sender can edit this message.");

            if (message.DeletedAt.HasValue)
                return response.SetBadRequest("Deleted messages cannot be edited.");

            message.Body = request.Body.Trim();
            message.EditedAt = DateTime.UtcNow;
            _uow.ChatMessages.Update(message);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapMessage(message));
        }

        public async Task<ApiResponse> DeleteMessageAsync(int messageId)
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();
            var message = await _uow.ChatMessages.GetAsync(m => m.MessageId == messageId, query => query.Include(m => m.Sender));
            if (message == null)
                return response.SetNotFound("Message not found.");

            if (message.SenderId != currentUser.Id)
                return response.SetBadRequest("Only the sender can delete this message.");

            message.DeletedAt = DateTime.UtcNow;
            message.Body = "[deleted]";
            _uow.ChatMessages.Update(message);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapMessage(message));
        }

        public async Task<ApiResponse> MarkConversationReadAsync(int conversationId)
        {
            var response = new ApiResponse();
            var currentUser = _claimService.GetUserClaim();
            var participant = await _uow.ChatParticipants.GetAsync(p => p.ConversationId == conversationId && p.UserId == currentUser.Id);
            if (participant == null)
                return response.SetBadRequest("You are not a participant in this conversation.");

            participant.LastReadAt = DateTime.UtcNow;
            _uow.ChatParticipants.Update(participant);
            await _uow.SaveChangeAsync();

            return response.SetOk("Conversation marked as read.");
        }

        private async Task<bool> IsParticipantAsync(int conversationId, int userId)
        {
            var participant = await _uow.ChatParticipants.GetAsync(p => p.ConversationId == conversationId && p.UserId == userId);
            return participant != null;
        }

        private static ConversationResponse MapConversation(ChatConversation conversation)
        {
            return new ConversationResponse
            {
                ConversationId = conversation.ConversationId,
                ProjectId = conversation.ProjectId,
                TaskId = conversation.TaskId,
                Title = conversation.Title,
                Type = conversation.Type,
                LastMessageAt = conversation.LastMessageAt,
                Participants = conversation.Participants.Select(p => new ConversationParticipantResponse
                {
                    UserId = p.UserId,
                    FullName = p.User == null ? null : $"{p.User.LastName} {p.User.FirstName}".Trim(),
                    Email = p.User?.Email,
                    JoinedAt = p.JoinedAt,
                    LastReadAt = p.LastReadAt
                }).ToList()
            };
        }

        private static MessageResponse MapMessage(ChatMessage message)
        {
            return new MessageResponse
            {
                MessageId = message.MessageId,
                ConversationId = message.ConversationId,
                SenderId = message.SenderId,
                SenderName = message.Sender == null ? null : $"{message.Sender.LastName} {message.Sender.FirstName}".Trim(),
                Body = message.Body,
                AttachmentUrl = message.AttachmentUrl,
                SentAt = message.SentAt,
                EditedAt = message.EditedAt,
                DeletedAt = message.DeletedAt
            };
        }
    }
}
