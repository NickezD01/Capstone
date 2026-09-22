using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using cpms_Application.Request.Warehouse;
using cpms_Application.Response;
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

        // POST: api/PurchaseOrders (retired - procurement writes return 410 Gone)
        [HttpPost]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request) => Gone();

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

        // PUT: api/PurchaseOrders/{id}/approve (retired)
        [HttpPut("{id}/approve")]
        [Authorize(Roles = "ADMIN,PM")]
        public IActionResult Approve(int id, [FromBody] PurchaseOrderActionRequest? request) => Gone();

        // PUT: api/PurchaseOrders/{id}/reject (retired)
        [HttpPut("{id}/reject")]
        [Authorize(Roles = "ADMIN,PM")]
        public IActionResult Reject(int id, [FromBody] PurchaseOrderActionRequest? request) => Gone();

        [HttpPost("from-shortages")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult CreateFromShortages([FromBody] CreatePurchaseOrderRequest request) => Gone();

        [HttpPost("{poId}/receive")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Receive(int poId, [FromBody] ReceivePurchaseOrderRequest request) => Gone();

        [HttpPost("{poId}/ship")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult Ship(int poId, [FromBody] PurchaseOrderActionRequest? request) => Gone();

        [HttpPost("{poId}/processing")]
        [Authorize(Roles = "WAREHOUSE_MANAGER")]
        public IActionResult MarkProcessing(int poId, [FromBody] PurchaseOrderActionRequest? request) => Gone();

        [HttpPost("{poId}/cancel")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public IActionResult Cancel(int poId, [FromBody] PurchaseOrderActionRequest? request) => Gone();

        private ObjectResult Gone() =>
            StatusCode((int)System.Net.HttpStatusCode.Gone,
                new ApiResponse().SetApiResponse(System.Net.HttpStatusCode.Gone, false,
                    "Purchase-order workflows are no longer supported. Material costs are managed through material requests and warehouse actual-cost accounting."));
    }
}
