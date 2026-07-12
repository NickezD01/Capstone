using cpms_Application.Interfaces;
using cpms_Domain;
using cpms_Domain.Models;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace cpms_Application.Services
{
    public class TeamsMeetingClient : ITeamsMeetingClient
    {
        private readonly HttpClient _httpClient;
        private readonly AppSetting _appSetting;

        public TeamsMeetingClient(HttpClient httpClient, AppSetting appSetting)
        {
            _httpClient = httpClient;
            _appSetting = appSetting;
        }

        public async Task<TeamsMeetingResult> CreateCalendarBackedMeetingAsync(Meeting meeting)
        {
            var graph = _appSetting.TeamsGraph;
            if (string.IsNullOrWhiteSpace(graph.TenantId) ||
                string.IsNullOrWhiteSpace(graph.ClientId) ||
                string.IsNullOrWhiteSpace(graph.ClientSecret) ||
                string.IsNullOrWhiteSpace(graph.OrganizerUserId))
            {
                return TeamsMeetingResult.Failed("Microsoft Graph TeamsGraph settings are not configured.");
            }

            var token = await GetAccessTokenAsync(graph.TenantId, graph.ClientId, graph.ClientSecret);
            if (string.IsNullOrWhiteSpace(token))
            {
                return TeamsMeetingResult.Failed("Could not acquire Microsoft Graph access token.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(graph.OrganizerUserId)}/events");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var body = new
            {
                subject = meeting.Subject,
                body = new
                {
                    contentType = "HTML",
                    content = string.IsNullOrWhiteSpace(meeting.Agenda) ? meeting.Subject : meeting.Agenda
                },
                start = new
                {
                    dateTime = meeting.StartDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = meeting.TimeZone
                },
                end = new
                {
                    dateTime = meeting.EndDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    timeZone = meeting.TimeZone
                },
                attendees = meeting.Participants.Select(p => new
                {
                    emailAddress = new
                    {
                        address = p.Email,
                        name = string.IsNullOrWhiteSpace(p.DisplayName) ? p.Email : p.DisplayName
                    },
                    type = p.Role == MeetingParticipantRole.OPTIONAL ? "optional" : "required"
                }).ToList(),
                isOnlineMeeting = true,
                onlineMeetingProvider = "teamsForBusiness"
            };

            request.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return TeamsMeetingResult.Failed($"Microsoft Graph returned {(int)response.StatusCode}: {responseText}");
            }

            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            var eventId = root.TryGetProperty("id", out var idProp) ? idProp.GetString() : null;
            string? joinUrl = null;
            string? onlineMeetingId = null;

            if (root.TryGetProperty("onlineMeeting", out var onlineMeeting) && onlineMeeting.ValueKind != JsonValueKind.Null)
            {
                if (onlineMeeting.TryGetProperty("joinUrl", out var joinUrlProp))
                {
                    joinUrl = joinUrlProp.GetString();
                }

                if (onlineMeeting.TryGetProperty("conferenceId", out var conferenceIdProp))
                {
                    onlineMeetingId = conferenceIdProp.GetString();
                }
            }

            return TeamsMeetingResult.Success(joinUrl, eventId, onlineMeetingId, responseText);
        }

        public async Task<TeamsMeetingResult> CancelCalendarBackedMeetingAsync(Meeting meeting, string? reason)
        {
            var graph = _appSetting.TeamsGraph;
            if (string.IsNullOrWhiteSpace(graph.TenantId) ||
                string.IsNullOrWhiteSpace(graph.ClientId) ||
                string.IsNullOrWhiteSpace(graph.ClientSecret) ||
                string.IsNullOrWhiteSpace(graph.OrganizerUserId) ||
                string.IsNullOrWhiteSpace(meeting.ExternalEventId))
            {
                return TeamsMeetingResult.Failed("Microsoft Graph settings or external event ID are missing.");
            }

            var token = await GetAccessTokenAsync(graph.TenantId, graph.ClientId, graph.ClientSecret);
            if (string.IsNullOrWhiteSpace(token))
            {
                return TeamsMeetingResult.Failed("Could not acquire Microsoft Graph access token.");
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://graph.microsoft.com/v1.0/users/{Uri.EscapeDataString(graph.OrganizerUserId)}/events/{Uri.EscapeDataString(meeting.ExternalEventId)}/cancel");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Content = new StringContent(JsonSerializer.Serialize(new { comment = reason ?? "Meeting cancelled from BuildSense." }), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request);
            var responseText = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
            {
                return TeamsMeetingResult.Failed($"Microsoft Graph returned {(int)response.StatusCode}: {responseText}");
            }

            return TeamsMeetingResult.Success(meeting.JoinUrl, meeting.ExternalEventId, meeting.ExternalOnlineMeetingId, responseText);
        }

        private async Task<string?> GetAccessTokenAsync(string tenantId, string clientId, string clientSecret)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"https://login.microsoftonline.com/{Uri.EscapeDataString(tenantId)}/oauth2/v2.0/token");
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["scope"] = "https://graph.microsoft.com/.default",
                ["grant_type"] = "client_credentials"
            });

            using var response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var responseText = await response.Content.ReadAsStringAsync();
            using var document = JsonDocument.Parse(responseText);
            return document.RootElement.TryGetProperty("access_token", out var tokenProp)
                ? tokenProp.GetString()
                : null;
        }
    }
}
