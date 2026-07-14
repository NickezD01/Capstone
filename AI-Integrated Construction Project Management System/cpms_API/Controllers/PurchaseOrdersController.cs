using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Warehouse;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu đăng nhập để thực hiện các thao tác đơn hàng
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly IPurchaseOrderService _poService;

        public PurchaseOrdersController(IPurchaseOrderService poService)
        {
            _poService = poService;
        }

        // POST: api/PurchaseOrders
        [HttpPost]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var response = await _poService.CreatePurchaseOrderAsync(request);

            if (!response.IsSuccess)
            {
                return BadRequest(response);
            }

            return Ok(response);
        }

        // GET: api/PurchaseOrders
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _poService.GetAllPurchaseOrdersAsync();
            return Ok(result);
        }

        // PUT: api/PurchaseOrders/{id}/approve
        [HttpPut("{id}/approve")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Approve(int id)
        {
            var result = await _poService.ApprovePurchaseOrderAsync(id);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // PUT: api/PurchaseOrders/{id}/reject
        // 🚀 BỔ SUNG: Endpoint xử lý từ chối đơn mua hàng công trình
        [HttpPut("{id}/reject")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Reject(int id)
        {
            var result = await _poService.RejectPurchaseOrderAsync(id);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        // POST: api/PurchaseOrders/{poId}/import?warehouseId=1
        [HttpPost("{poId}/import")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Import(int poId, [FromQuery] int warehouseId)
        {
            var result = await _poService.ImportToWarehouseAsync(poId, warehouseId);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }

        [HttpPost("from-shortages")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> CreateFromShortages([FromBody] CreatePurchaseOrderRequest request)
        {
            if (request.Items.Any(item => !item.RequestItemId.HasValue))
                return BadRequest("Every shortage line must include RequestItemId.");
            var response = await _poService.CreatePurchaseOrderAsync(request);
            return response.IsSuccess ? Ok(response) : StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{poId}/receive")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Receive(int poId, [FromBody] ReceivePurchaseOrderRequest request)
        {
            var result = await _poService.ReceivePurchaseOrderAsync(poId, request);
            return result.IsSuccess ? Ok(result) : StatusCode((int)result.StatusCode, result);
        }
    }
}
