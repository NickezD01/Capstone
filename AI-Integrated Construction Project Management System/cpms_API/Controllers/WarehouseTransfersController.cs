using cpms_Application.Interfaces;
using cpms_Application.Request.WarehouseTransfer;
using cpms_Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WarehouseTransfersController : ControllerBase
    {
        private readonly IWarehouseTransferService _service;
        public WarehouseTransfersController(IWarehouseTransferService service) => _service = service;

        [HttpPost]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Create([FromBody] CreateWarehouseTransferRequest request)
        {
            var response = await _service.CreateAsync(request);
            if (response.IsSuccess && response.Result is cpms_Application.Response.WarehouseTransfer.WarehouseTransferResponse created)
                return CreatedAtAction(nameof(GetById), new { id = created.TransferId }, response);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll() => ToResult(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetById(int id) => ToResult(await _service.GetByIdAsync(id));

        [HttpPut("{id:int}/approve")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Approve(int id) => ToResult(await _service.ApproveAsync(id));

        [HttpPut("{id:int}/reject")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Reject(int id) => ToResult(await _service.RejectAsync(id));

        [HttpPost("{id:int}/ship")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Ship(int id) => ToResult(await _service.ShipAsync(id));

        [HttpPost("{id:int}/receive")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Receive(int id, [FromBody] ReceiveWarehouseTransferRequest? request) =>
            ToResult(await _service.ReceiveAsync(id, request));

        [HttpPut("{id:int}/cancel")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Cancel(int id) => ToResult(await _service.CancelAsync(id));

        private ObjectResult ToResult(ApiResponse response) => StatusCode((int)response.StatusCode, response);
    }
}
