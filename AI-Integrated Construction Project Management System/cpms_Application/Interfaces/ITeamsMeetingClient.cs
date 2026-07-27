using cpms_Domain.Models;

namespace cpms_Application.Interfaces
{
    public interface ITeamsMeetingClient
    {
        Task<TeamsMeetingResult> CreateCalendarBackedMeetingAsync(Meeting meeting);
        Task<TeamsMeetingResult> CancelCalendarBackedMeetingAsync(Meeting meeting, string? reason);
    }

    public class TeamsMeetingResult
    {
        public bool IsSuccess { get; set; }
        public string? JoinUrl { get; set; }
        public string? ExternalEventId { get; set; }
        public string? ExternalOnlineMeetingId { get; set; }
        public string? RawResponse { get; set; }
        public string? ErrorMessage { get; set; }

        public static TeamsMeetingResult Success(string? joinUrl, string? eventId, string? onlineMeetingId, string? rawResponse)
        {
            return new TeamsMeetingResult
            {
                IsSuccess = true,
                JoinUrl = joinUrl,
                ExternalEventId = eventId,
                ExternalOnlineMeetingId = onlineMeetingId,
                RawResponse = rawResponse
            };
        }

        public static TeamsMeetingResult Failed(string errorMessage)
        {
            return new TeamsMeetingResult { IsSuccess = false, ErrorMessage = errorMessage };
        }
    }
}
