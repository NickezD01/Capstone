using cpms_Application.Interfaces;
using cpms_Application.Request.Supplier;
using cpms_Application.Request.SupplierRecommendation;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;
        private readonly ISupplierRecommendationService _supplierRecommendationService;

        public SuppliersController(ISupplierService supplierService, ISupplierRecommendationService supplierRecommendationService)
        {
            _supplierService = supplierService;
            _supplierRecommendationService = supplierRecommendationService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateSupplierRequest request)
        {
            var response = await _supplierService.CreateSupplierAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var response = await _supplierService.GetAllSuppliersAsync();
            return Ok(response);
        }

        [HttpPost("recommendations/balanced")]
        public async Task<IActionResult> RecommendBalancedSuppliers([FromBody] BalancedSupplierRecommendationRequest request)
        {
            var response = await _supplierRecommendationService.RecommendBalancedSuppliersAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }
    }
}
