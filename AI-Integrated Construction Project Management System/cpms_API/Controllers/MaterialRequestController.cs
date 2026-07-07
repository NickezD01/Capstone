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

        // POST: api/materialrequest (Tạo phiếu + Giữ kho tạm)
        [HttpPost]
        public async Task<IActionResult> CreateMaterialRequest([FromBody] CreateMaterialRequest request)
        {
            var response = await _materialRequestService.CreateRequestAsync(request);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // 🚀 BỔ SUNG: PUT: api/materialrequest/{requestId}/approve
        [HttpPut("{requestId}/approve")]
        public async Task<IActionResult> ApproveRequest(int requestId)
        {
            var response = await _materialRequestService.ApproveRequestAsync(requestId);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }

        // 🚀 BỔ SUNG: PUT: api/materialrequest/{requestId}/reject
        [HttpPut("{requestId}/reject")]
        public async Task<IActionResult> RejectRequest(int requestId)
        {
            var response = await _materialRequestService.RejectRequestAsync(requestId);
            if (!response.IsSuccess) return BadRequest(response);
            return Ok(response);
        }
    }
}