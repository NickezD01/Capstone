using cpms_Application.Interfaces;
using cpms_Application.Request.Supplier;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
        {
            var response = await _supplierService.CreateSupplierAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetAll()
        {
            var response = await _supplierService.GetAllSuppliersAsync();
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{supplierId:int}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetById(int supplierId)
        {
            var response = await _supplierService.GetSupplierByIdAsync(supplierId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{supplierId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Update(int supplierId, [FromBody] UpdateSupplierRequest request)
        {
            var response = await _supplierService.UpdateSupplierAsync(supplierId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpDelete("{supplierId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Deactivate(int supplierId)
        {
            var response = await _supplierService.DeactivateSupplierAsync(supplierId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
