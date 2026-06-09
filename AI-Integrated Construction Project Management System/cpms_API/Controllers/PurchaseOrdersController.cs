using cpms_Application.Interfaces;
using cpms_Application.Request.PurchaseOrder;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize] // Yêu cầu đăng nhập để thực hiện mua sắm
    public class PurchaseOrdersController : ControllerBase
    {
        private readonly IPurchaseOrderService _poService;

        public PurchaseOrdersController(IPurchaseOrderService poService)
        {
            _poService = poService;
        }

        // POST: api/purchaseorders
        [HttpPost]
        public async Task<IActionResult> CreatePurchaseOrder([FromBody] CreatePurchaseOrderRequest request)
        {
            // Kiểm tra tính hợp lệ của dữ liệu thông qua FluentValidation (đã cấu hình trong Program.cs)
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

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var result = await _poService.GetAllPurchaseOrdersAsync();
            return Ok(result);
        }

        [HttpPut("{id}/approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var result = await _poService.ApprovePurchaseOrderAsync(id);
            return result.IsSuccess ? Ok(result) : BadRequest(result);
        }
    }
}