using cpms_Application.Interfaces;
using cpms_Application.Request.SupplierCatalog;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CatalogsController : ControllerBase
    {
        private readonly ICatalogService _catalogService;

        public CatalogsController(ICatalogService catalogService)
        {
            _catalogService = catalogService;
        }

        [HttpPost]
        public async Task<IActionResult> AddMaterial([FromBody] CreateCatalogRequest request)
        {
            var response = await _catalogService.AddMaterialToCatalogAsync(request);
            return response.IsSuccess ? Ok(response) : BadRequest(response);
        }
    }
}
