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
            return Ok(await _warehouseService.CreateWarehouseAsync(request));
        }

        [HttpGet("{warehouseId}/inventory/{variantId}")]
        public async Task<IActionResult> GetInventory(int warehouseId, int variantId)
            => Ok(await _warehouseService.GetInventoryAsync(warehouseId, variantId));

        [HttpPost("inventory/adjust")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> AdjustInventory([FromBody] InventoryAdjustmentRequest request)
            => Ok(await _warehouseService.AdjustInventoryAsync(request));

        [HttpGet("inventory/transactions")]
        public async Task<IActionResult> GetTransactions([FromQuery] int? warehouseId, [FromQuery] int? variantId)
            => Ok(await _warehouseService.GetTransactionsAsync(warehouseId, variantId));

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _warehouseService.GetAllWarehousesAsync());
        }

        [HttpGet("{id}/inventory")]
        public async Task<IActionResult> GetWarehouseInventory(int id)
        {
            var result = await _warehouseService.GetWarehouseInventoryAsync(id);
            return Ok(result);
        }
    }
}
