using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Request.MaterialRequest;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MaterialRequestController : ControllerBase
    {
        private readonly IMaterialRequestService _materialRequestService;

        public MaterialRequestController(IMaterialRequestService materialRequestService)
        {
            _materialRequestService = materialRequestService;
        }

        // POST: api/materialrequest (Tạo phiếu thủ công/phát sinh + Giữ kho tạm)
        [HttpPost]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateMaterialRequest([FromBody] CreateMaterialRequest request)
        {
            var response = await _materialRequestService.CreateRequestAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        // POST: api/materialrequest/task/{taskId} (Tự động bốc định mức từ TaskId)
        [HttpPost("task/{taskId}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateRequestFromTask(int taskId)
        {
            var response = await _materialRequestService.CreateRequestByTaskIdAsync(taskId);
            return StatusCode((int)response.StatusCode, response);
        }

        // PUT: api/materialrequest/{requestId}/approve
        [HttpPut("{requestId}/approve")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ApproveRequest(int requestId, [FromBody] ApproveMaterialRequest request)
        {
            var response = await _materialRequestService.ApproveRequestAsync(requestId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        // PUT: api/materialrequest/{requestId}/reject
        [HttpPut("{requestId}/reject")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> RejectRequest(int requestId, [FromBody] RejectMaterialRequest? request)
        {
            var response = await _materialRequestService.RejectRequestAsync(requestId, request ?? new RejectMaterialRequest());
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{requestId}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> UpdatePendingRequest(int requestId, [FromBody] UpdatePendingMaterialRequest request)
        {
            var response = await _materialRequestService.UpdatePendingRequestAsync(requestId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{requestId}/cancel")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CancelPendingRequest(int requestId, [FromBody] CancelMaterialRequest request)
        {
            var response = await _materialRequestService.CancelPendingRequestAsync(requestId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{requestId}/issue")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> IssueRequest(int requestId)
        {
            var response = await _materialRequestService.IssueRequestAsync(requestId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{requestId}/release")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ReleaseRequest(int requestId)
        {
            var response = await _materialRequestService.ReleaseRequestAsync(requestId);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/materialrequest (Lấy toàn bộ phiếu)
        [HttpGet]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAllRequests()
        {
            var response = await _materialRequestService.GetAllRequestsAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/materialrequest/{requestId} (Lấy chi tiết phiếu)
        [HttpGet("{requestId}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetRequestById(int requestId)
        {
            var response = await _materialRequestService.GetRequestByIdAsync(requestId);
            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/materialrequest/project/{projectId} (Lấy phiếu theo Project)
        [HttpGet("project/{projectId}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetRequestsByProject(int projectId)
        {
            var response = await _materialRequestService.GetRequestsByProjectAsync(projectId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
