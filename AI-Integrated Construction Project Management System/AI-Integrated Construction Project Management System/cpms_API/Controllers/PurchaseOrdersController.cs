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
                return StatusCode((int)response.StatusCode, response);
            }

            return StatusCode((int)response.StatusCode, response);
        }

        // GET: api/PurchaseOrders
        [HttpGet]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll()
        {
            var result = await _poService.GetAllPurchaseOrdersAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("{id:int}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetById(int id)
        {
            var result = await _poService.GetPurchaseOrderByIdAsync(id);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpGet("shortages")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetProcurementShortages()
        {
            var result = await _poService.GetProcurementShortagesAsync();
            return StatusCode((int)result.StatusCode, result);
        }

        // PUT: api/PurchaseOrders/{id}/approve
        [HttpPut("{id}/approve")]
        [Authorize(Roles = "ADMIN,PM")]
        public async Task<IActionResult> Approve(int id, [FromBody] PurchaseOrderActionRequest? request)
        {
            var result = await _poService.ApprovePurchaseOrderAsync(id, request);
            return StatusCode((int)result.StatusCode, result);
        }

        // PUT: api/PurchaseOrders/{id}/reject
        // 🚀 BỔ SUNG: Endpoint xử lý từ chối đơn mua hàng công trình
        [HttpPut("{id}/reject")]
        [Authorize(Roles = "ADMIN,PM")]
        public async Task<IActionResult> Reject(int id, [FromBody] PurchaseOrderActionRequest? request)
        {
            var result = await _poService.RejectPurchaseOrderAsync(id, request);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("from-shortages")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> CreateFromShortages([FromBody] CreatePurchaseOrderRequest request)
        {
            if (request.Items.Any(item => !item.RequestItemId.HasValue))
                return BadRequest("Every shortage line must include RequestItemId.");
            var response = await _poService.CreatePurchaseOrderAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPost("{poId}/receive")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Receive(int poId, [FromBody] ReceivePurchaseOrderRequest request)
        {
            var result = await _poService.ReceivePurchaseOrderAsync(poId, request);
            return result.IsSuccess ? Ok(result) : StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("{poId}/ship")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Ship(int poId, [FromBody] PurchaseOrderActionRequest? request)
        {
            var result = await _poService.MarkShippedAsync(poId, request);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("{poId}/processing")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public async Task<IActionResult> MarkProcessing(int poId, [FromBody] PurchaseOrderActionRequest? request)
        {
            var result = await _poService.MarkProcessingAsync(poId, request);
            return StatusCode((int)result.StatusCode, result);
        }

        [HttpPost("{poId}/cancel")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> Cancel(int poId, [FromBody] PurchaseOrderActionRequest? request)
        {
            var result = await _poService.CancelPurchaseOrderAsync(poId, request);
            return StatusCode((int)result.StatusCode, result);
        }
    }
}
