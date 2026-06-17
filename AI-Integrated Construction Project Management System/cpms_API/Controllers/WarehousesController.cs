using cpms_Application.Interfaces;
using cpms_Application.Request.Warehouse;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class WarehousesController : ControllerBase
    {
        private readonly IWarehouseService _warehouseService;

        public WarehousesController(IWarehouseService warehouseService)
        {
            _warehouseService = warehouseService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWarehouseRequest request)
        {
            return Ok(await _warehouseService.CreateWarehouseAsync(request));
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            return Ok(await _warehouseService.GetAllWarehousesAsync());
        }
    }
}
