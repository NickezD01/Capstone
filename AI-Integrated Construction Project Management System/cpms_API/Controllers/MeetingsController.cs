using cpms_Application.Interfaces;
using cpms_Application.Request.Meeting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MeetingsController : ControllerBase
    {
        private readonly IMeetingService _meetingService;

        public MeetingsController(IMeetingService meetingService)
        {
            _meetingService = meetingService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateMeeting([FromBody] CreateMeetingRequest request)
        {
            var response = await _meetingService.CreateMeetingAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetProjectMeetings(int projectId)
        {
            var response = await _meetingService.GetProjectMeetingsAsync(projectId);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpGet("{meetingId}")]
        public async Task<IActionResult> GetMeetingById(int meetingId)
        {
            var response = await _meetingService.GetMeetingByIdAsync(meetingId);
            return response.IsSuccess ? Ok(response) : NotFound(response);
        }

        [HttpPut("{meetingId}/cancel")]
        public async Task<IActionResult> CancelMeeting(int meetingId, [FromBody] CancelMeetingRequest request)
        {
            var response = await _meetingService.CancelMeetingAsync(meetingId, request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }
    }
}
