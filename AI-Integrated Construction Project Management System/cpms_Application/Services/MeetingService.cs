using cpms_Application.Interfaces;
using cpms_Application.Request.Meeting;
using cpms_Application.Response;
using cpms_Application.Response.Meeting;
using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace cpms_Application.Services
{
    public class MeetingService : IMeetingService
    {
        private readonly IUnitOfWork _uow;
        private readonly IClaimService _claimService;
        private readonly ITeamsMeetingClient _teamsMeetingClient;

        public MeetingService(IUnitOfWork uow, IClaimService claimService, ITeamsMeetingClient teamsMeetingClient)
        {
            _uow = uow;
            _claimService = claimService;
            _teamsMeetingClient = teamsMeetingClient;
        }

        public async Task<ApiResponse> CreateMeetingAsync(CreateMeetingRequest request)
        {
            var response = new ApiResponse();
            if (string.IsNullOrWhiteSpace(request.Subject))
                return response.SetBadRequest("Meeting subject is required.");

            if (request.EndDateTime <= request.StartDateTime)
                return response.SetBadRequest("Meeting end time must be after start time.");

            if (request.Participants == null || request.Participants.Count == 0)
                return response.SetBadRequest("At least one meeting participant is required.");

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

            var meeting = new Meeting
            {
                ProjectId = request.ProjectId,
                TaskId = request.TaskId,
                OrganizerId = currentUser.Id,
                Subject = request.Subject.Trim(),
                Agenda = request.Agenda,
                StartDateTime = request.StartDateTime,
                EndDateTime = request.EndDateTime,
                TimeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone,
                Status = request.ScheduleWithTeams ? MeetingStatus.DRAFT : MeetingStatus.SCHEDULED
            };

            foreach (var participantRequest in request.Participants)
            {
                if (string.IsNullOrWhiteSpace(participantRequest.Email))
                    return response.SetBadRequest("Participant email is required.");

                if (participantRequest.UserId.HasValue)
                {
                    var user = await _uow.UserAccounts.GetByIdAsync(participantRequest.UserId.Value);
                    if (user == null)
                        return response.SetBadRequest($"UserId {participantRequest.UserId.Value} does not exist.");
                }

                meeting.Participants.Add(new MeetingParticipant
                {
                    UserId = participantRequest.UserId,
                    Email = participantRequest.Email.Trim(),
                    DisplayName = participantRequest.DisplayName,
                    Role = participantRequest.Role
                });
            }

            if (request.ScheduleWithTeams)
            {
                var teamsResult = await _teamsMeetingClient.CreateCalendarBackedMeetingAsync(meeting);
                if (teamsResult.IsSuccess)
                {
                    meeting.Status = MeetingStatus.SCHEDULED;
                    meeting.JoinUrl = teamsResult.JoinUrl;
                    meeting.ExternalEventId = teamsResult.ExternalEventId;
                    meeting.ExternalOnlineMeetingId = teamsResult.ExternalOnlineMeetingId;
                    meeting.GraphResponse = teamsResult.RawResponse;
                }
                else
                {
                    meeting.Status = MeetingStatus.FAILED;
                    meeting.FailureReason = teamsResult.ErrorMessage;
                }
            }

            await _uow.Meetings.AddAsync(meeting);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapMeeting(meeting));
        }

        public async Task<ApiResponse> GetProjectMeetingsAsync(int projectId)
        {
            var response = new ApiResponse();
            var meetings = await _uow.Meetings.GetAllAsync(
                filter: m => m.ProjectId == projectId,
                include: query => query
                    .Include(m => m.Organizer)
                    .Include(m => m.Participants)
            );

            return response.SetOk(meetings
                .OrderByDescending(m => m.StartDateTime)
                .Select(MapMeeting)
                .ToList());
        }

        public async Task<ApiResponse> GetMeetingByIdAsync(int meetingId)
        {
            var response = new ApiResponse();
            var meeting = await _uow.Meetings.GetAsync(
                m => m.MeetingId == meetingId,
                query => query
                    .Include(m => m.Organizer)
                    .Include(m => m.Participants)
            );

            if (meeting == null)
                return response.SetNotFound("Meeting not found.");

            return response.SetOk(MapMeeting(meeting));
        }

        public async Task<ApiResponse> CancelMeetingAsync(int meetingId, CancelMeetingRequest request)
        {
            var response = new ApiResponse();
            var meeting = await _uow.Meetings.GetAsync(
                m => m.MeetingId == meetingId,
                query => query
                    .Include(m => m.Organizer)
                    .Include(m => m.Participants)
            );

            if (meeting == null)
                return response.SetNotFound("Meeting not found.");

            if (meeting.Status == MeetingStatus.CANCELLED)
                return response.SetBadRequest("Meeting is already cancelled.");

            if (!string.IsNullOrWhiteSpace(meeting.ExternalEventId))
            {
                var cancelResult = await _teamsMeetingClient.CancelCalendarBackedMeetingAsync(meeting, request.Reason);
                if (!cancelResult.IsSuccess)
                {
                    meeting.FailureReason = cancelResult.ErrorMessage;
                    _uow.Meetings.Update(meeting);
                    await _uow.SaveChangeAsync();
                    return response.SetBadRequest(cancelResult.ErrorMessage);
                }
            }

            meeting.Status = MeetingStatus.CANCELLED;
            _uow.Meetings.Update(meeting);
            await _uow.SaveChangeAsync();

            return response.SetOk(MapMeeting(meeting));
        }

        private static MeetingResponse MapMeeting(Meeting meeting)
        {
            return new MeetingResponse
            {
                MeetingId = meeting.MeetingId,
                ProjectId = meeting.ProjectId,
                TaskId = meeting.TaskId,
                OrganizerId = meeting.OrganizerId,
                OrganizerName = meeting.Organizer == null ? null : $"{meeting.Organizer.LastName} {meeting.Organizer.FirstName}".Trim(),
                Subject = meeting.Subject,
                Agenda = meeting.Agenda,
                StartDateTime = meeting.StartDateTime,
                EndDateTime = meeting.EndDateTime,
                TimeZone = meeting.TimeZone,
                Status = meeting.Status,
                JoinUrl = meeting.JoinUrl,
                ExternalEventId = meeting.ExternalEventId,
                ExternalOnlineMeetingId = meeting.ExternalOnlineMeetingId,
                FailureReason = meeting.FailureReason,
                Participants = meeting.Participants.Select(p => new MeetingParticipantResponse
                {
                    UserId = p.UserId,
                    Email = p.Email,
                    DisplayName = p.DisplayName,
                    Role = p.Role
                }).ToList()
            };
        }
    }
}
