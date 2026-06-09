using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class MaterialsController : ControllerBase
    {
        private readonly IMaterialService _service;
        public MaterialsController(IMaterialService service) => _service = service;

        [HttpPost]
        public async Task<IActionResult> Create(CreateMaterialRequest request)
            => Ok(await _service.CreateMaterialAsync(request));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllMaterialsAsync());
    }
}
