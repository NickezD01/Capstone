using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class WarehousesController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;

        public WarehousesController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request)
        {
            var response = await _warehouseService.CreateWarehouseAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{warehouseId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Update(int warehouseId, [FromBody] UpdateWarehouseRequest request)
        {
            var response = await _warehouseService.UpdateWarehouseAsync(warehouseId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{warehouseId}/inventory/{variantId}")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetInventory(int warehouseId, int variantId)
        {
            var response = await _warehouseService.GetInventoryAsync(warehouseId, variantId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("inventory/adjust")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> AdjustInventory([FromBody] InventoryAdjustmentRequest request)
        {
            var response = await _warehouseService.AdjustInventoryAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("inventory/adjustments")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetInventoryAdjustments([FromQuery] string? status)
        {
            var response = await _warehouseService.GetInventoryAdjustmentsAsync(status);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("inventory/adjustments/{adjustmentId:int}/approve")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ApproveInventoryAdjustment(int adjustmentId, ReviewInventoryAdjustmentRequest request)
        {
            var response = await _warehouseService.ReviewInventoryAdjustmentAsync(adjustmentId, true, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("inventory/adjustments/{adjustmentId:int}/reject")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> RejectInventoryAdjustment(int adjustmentId, ReviewInventoryAdjustmentRequest request)
        {
            var response = await _warehouseService.ReviewInventoryAdjustmentAsync(adjustmentId, false, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("inventory/return")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> ReturnInventory([FromBody] InventoryReturnRequest request)
        {
            var response = await _warehouseService.ReturnInventoryAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("inventory/transactions")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetTransactions([FromQuery] int? warehouseId, [FromQuery] int? variantId)
        {
            var response = await _warehouseService.GetTransactionsAsync(warehouseId, variantId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("physical-counts")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> StartPhysicalCount(StartPhysicalCountRequest request)
        {
            var response = await _warehouseService.StartPhysicalCountAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("physical-counts/{sessionId:int}/submit")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> SubmitPhysicalCount(int sessionId, SubmitPhysicalCountRequest request)
        {
            var response = await _warehouseService.SubmitPhysicalCountAsync(sessionId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("physical-counts/{sessionId:int}/approve")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> ApprovePhysicalCount(int sessionId, ReviewPhysicalCountRequest request)
        {
            var response = await _warehouseService.ReviewPhysicalCountAsync(sessionId, true, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("physical-counts/{sessionId:int}/reject")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> RejectPhysicalCount(int sessionId, ReviewPhysicalCountRequest request)
        {
            var response = await _warehouseService.ReviewPhysicalCountAsync(sessionId, false, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("physical-counts")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetPhysicalCounts([FromQuery] int? warehouseId, [FromQuery] string? status)
        {
            var response = await _warehouseService.GetPhysicalCountsAsync(warehouseId, status);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _warehouseService.GetAllWarehousesAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetById(int id)
        {
            var response = await _warehouseService.GetWarehouseByIdAsync(id);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{id}/inventory")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetWarehouseInventory(int id)
        {
            var result = await _warehouseService.GetWarehouseInventoryAsync(id);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
