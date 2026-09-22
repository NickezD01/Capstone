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

        // Warehouse-transfer writes are retired: the application now uses a single active warehouse.
        [HttpPost]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Create([FromBody] CreateWarehouseTransferRequest request) => Gone();

        [HttpGet]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll() => ToResult(await _service.GetAllAsync());

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetById(int id) => ToResult(await _service.GetByIdAsync(id));

        [HttpPut("{id:int}/approve")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public IActionResult Approve(int id) => Gone();

        [HttpPut("{id:int}/reject")]
        [Authorize(Roles = "ADMIN,WAREHOUSE_MANAGER")]
        public IActionResult Reject(int id) => Gone();

        [HttpPost("{id:int}/ship")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Ship(int id) => Gone();

        [HttpPost("{id:int}/receive")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Receive(int id, [FromBody] ReceiveWarehouseTransferRequest? request) => Gone();

        [HttpPut("{id:int}/cancel")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Cancel(int id) => Gone();

        private ObjectResult Gone() =>
            StatusCode((int)System.Net.HttpStatusCode.Gone,
                new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Gone, false,
                    "Warehouse transfers are no longer supported. The application uses a single active warehouse."));

        private ObjectResult ToResult(ApiResponse response) => StatusCode((int)response.StatusCode, response);
    }
}
