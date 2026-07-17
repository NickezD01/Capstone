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
    }
}
