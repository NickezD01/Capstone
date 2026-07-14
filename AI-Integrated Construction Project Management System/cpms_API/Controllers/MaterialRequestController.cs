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
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // POST: api/materialrequest/task/{taskId} (Tự động bốc định mức từ TaskId)
        [HttpPost("task/{taskId}")]
        [Authorize(Roles = "PM")]
        public async Task<IActionResult> CreateRequestFromTask(int taskId)
        {
            var response = await _materialRequestService.CreateRequestByTaskIdAsync(taskId);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // PUT: api/materialrequest/{requestId}/approve
        [HttpPut("{requestId}/approve")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ApproveRequest(int requestId, [FromBody] ApproveMaterialRequest request)
        {
            var response = await _materialRequestService.ApproveRequestAsync(requestId, request);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // PUT: api/materialrequest/{requestId}/reject
        [HttpPut("{requestId}/reject")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> RejectRequest(int requestId, [FromBody] RejectMaterialRequest? request)
        {
            var response = await _materialRequestService.RejectRequestAsync(requestId, request ?? new RejectMaterialRequest());
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        [HttpPut("{requestId}/reserve")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ReserveRequest(int requestId, [FromBody] ApproveMaterialRequest request)
            => Ok(await _materialRequestService.ApproveRequestAsync(requestId, request));

        [HttpPut("{requestId}/issue")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> IssueRequest(int requestId)
            => Ok(await _materialRequestService.IssueRequestAsync(requestId));

        [HttpPut("{requestId}/release")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ReleaseRequest(int requestId)
            => Ok(await _materialRequestService.ReleaseRequestAsync(requestId));

        // GET: api/materialrequest (Lấy toàn bộ phiếu)
        [HttpGet]
        public async Task<IActionResult> GetAllRequests()
        {
            var response = await _materialRequestService.GetAllRequestsAsync();
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // GET: api/materialrequest/{requestId} (Lấy chi tiết phiếu)
        [HttpGet("{requestId}")]
        public async Task<IActionResult> GetRequestById(int requestId)
        {
            var response = await _materialRequestService.GetRequestByIdAsync(requestId);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // GET: api/materialrequest/project/{projectId} (Lấy phiếu theo Project)
        [HttpGet("project/{projectId}")]
        public async Task<IActionResult> GetRequestsByProject(int projectId)
        {
            var response = await _materialRequestService.GetRequestsByProjectAsync(projectId);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }
    }
}
