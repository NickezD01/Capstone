using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    using cpms_Application.Interfaces;
    using cpms_Application.Request.Material;
    using Microsoft.AspNetCore.Authorization;
    using Microsoft.AspNetCore.Mvc;

    namespace cpms_API.Controllers
    {
        [Route("api/[controller]")]
        [ApiController]
        [Authorize]
        public class MaterialsController : ControllerBase
        {
            private readonly IMaterialService _service;
            public MaterialsController(IMaterialService service) => _service = service;

            // 1. Tạo mới vật tư
            [HttpPost]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> Create([FromBody] MaterialRequest request)
                => Ok(await _service.CreateMaterialAsync(request));

            // 2. Lấy toàn bộ danh sách vật tư
            [HttpGet]
            public async Task<IActionResult> GetAll()
                => Ok(await _service.GetAllMaterialsAsync());

            // 3. Lấy chi tiết vật tư theo ID
            [HttpGet("{id}")]
            public async Task<IActionResult> GetById(int id)
                => Ok(await _service.GetMaterialByIdAsync(id));

            // 4. Cập nhật thông tin vật tư
            [HttpPut("{id}")]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> Update(int id, [FromBody] UpdateMaterialRequest request)
                => Ok(await _service.UpdateMaterialAsync(id, request));

            // 5. Xóa vật tư
            [HttpDelete("{id}")]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> Delete(int id)
                => Ok(await _service.DeleteMaterialAsync(id));

            [HttpPost("variants")]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> CreateVariant([FromBody] MaterialVariantRequest request)
                => Ok(await _service.CreateVariantAsync(request));

            [HttpGet("{materialId}/variants")]
            public async Task<IActionResult> GetVariants(int materialId)
                => Ok(await _service.GetVariantsByMaterialAsync(materialId));

            [HttpPut("variants/{variantId}")]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> UpdateVariant(int variantId, [FromBody] MaterialVariantRequest request)
                => Ok(await _service.UpdateVariantAsync(variantId, request));

            [HttpDelete("variants/{variantId}")]
            [Authorize(Roles = "ADMIN")]
            public async Task<IActionResult> DeleteVariant(int variantId)
                => Ok(await _service.DeleteVariantAsync(variantId));
        }
    }
}
