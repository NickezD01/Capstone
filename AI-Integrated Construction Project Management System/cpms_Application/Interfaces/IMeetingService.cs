using cpms_Application.Request.Meeting;
using cpms_Application.Response;

namespace cpms_Application.Interfaces
{
    public interface IMeetingService
    {
        Task<ApiResponse> CreateMeetingAsync(CreateMeetingRequest request);
        Task<ApiResponse> GetProjectMeetingsAsync(int projectId);
        Task<ApiResponse> GetMeetingByIdAsync(int meetingId);
        Task<ApiResponse> CancelMeetingAsync(int meetingId, CancelMeetingRequest request);
    }
}
