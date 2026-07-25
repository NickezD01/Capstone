using cpms_Application.Interfaces;
using cpms_Application.Request.SupplierCatalog;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CatalogsController : ControllerBase
    {
        private readonly ICatalogService _catalogService;

        public CatalogsController(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> AddMaterial([FromBody] CreateCatalogRequest request)
        {
            var response = await _catalogService.AddMaterialToCatalogAsync(request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetOffers(
            [FromQuery] int? supplierId,
            [FromQuery] int? variantId,
            [FromQuery] bool availableOnly = true)
        {
            var response = await _catalogService.GetCatalogOffersAsync(supplierId, variantId, availableOnly);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpGet("{catalogId:int}")]
        [Authorize(Roles = "ADMIN,PM,WAREHOUSE_MANAGER")]
        public async Task<IActionResult> GetOfferById(int catalogId)
        {
            var response = await _catalogService.GetCatalogOfferByIdAsync(catalogId);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpPut("{catalogId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> UpdateOffer(int catalogId, [FromBody] UpdateCatalogRequest request)
        {
            var response = await _catalogService.UpdateCatalogOfferAsync(catalogId, request);
            return StatusCode((int)response.StatusCode, response);
        }

        [HttpDelete("{catalogId:int}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> DeactivateOffer(int catalogId)
        {
            var response = await _catalogService.DeactivateCatalogOfferAsync(catalogId);
            return StatusCode((int)response.StatusCode, response);
        }
    }
}
