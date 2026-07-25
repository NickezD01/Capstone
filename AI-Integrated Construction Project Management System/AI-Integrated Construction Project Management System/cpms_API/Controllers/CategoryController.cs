using cpms_Application.Interfaces;
using cpms_Application.Request.Category;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace cpms_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _service;
        public CategoriesController(ICategoryService service) => _service = service;

        [HttpPost]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Create(CreateCategoryRequest request)
            => ToResult(await _service.CreateCategoryAsync(request));

        [HttpGet]
        public async Task<IActionResult> GetAll()
            => ToResult(await _service.GetAllCategoriesAsync());

        [HttpGet("{id}")]
        public async Task<IActionResult> GetById(int id)
            => ToResult(await _service.GetCategoryByIdAsync(id));

        [HttpPut("{id}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Update(int id, UpdateCategoryRequest request)
            => ToResult(await _service.UpdateCategoryAsync(id, request));

        [HttpDelete("{id}")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Delete(int id)
            => ToResult(await _service.DeleteCategoryAsync(id));

        private ObjectResult ToResult(cpms_Application.Response.ApiResponse response) => StatusCode((int)response.StatusCode, response);
    }
}
