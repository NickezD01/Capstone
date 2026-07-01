using cpms_Application.Interfaces;
using cpms_Application.Request.Category;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _service;
        public CategoriesController(ICategoryService service) => _service = service;

        [HttpPost]
        public async Task<IActionResult> Create(CreateCategoryRequest request)
            => Ok(await _service.CreateCategoryAsync(request));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => Ok(await _service.GetAllCategoriesAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
            => Ok(await _service.GetCategoryByIdAsync(id));

        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, UpdateCategoryRequest request)
            => Ok(await _service.UpdateCategoryAsync(id, request));

        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
            => Ok(await _service.DeleteCategoryAsync(id));
    }
}
