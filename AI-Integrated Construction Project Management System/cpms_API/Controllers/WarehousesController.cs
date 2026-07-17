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

        [HttpGet("inventory/transactions")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetTransactions([FromQuery] int? warehouseId, [FromQuery] int? variantId)
        {
            var response = await _warehouseService.GetTransactionsAsync(warehouseId, variantId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _warehouseService.GetAllWarehousesAsync();
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
