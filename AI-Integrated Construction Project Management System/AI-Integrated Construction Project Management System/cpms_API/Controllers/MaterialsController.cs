using cpms_Application.Interfaces;
using cpms_Application.Request.Material;
using cpms_Application.Response;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace cpms_API.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class MaterialsController : ControllerBase
{
    private readonly IMaterialService _service;

    public MaterialsController(IMaterialService service) => _service = service;

    [HttpPost]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Create([FromBody] MaterialRequest request) =>
        ToResult(await _service.CreateMaterialAsync(request));

    [HttpGet]
    public async Task<IActionResult> GetAll() =>
        ToResult(await _service.GetAllMaterialsAsync());

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id) =>
        ToResult(await _service.GetMaterialByIdAsync(id));

    [HttpPut("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateMaterialRequest request) =>
        ToResult(await _service.UpdateMaterialAsync(id, request));

    [HttpDelete("{id}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> Delete(int id) =>
        ToResult(await _service.DeleteMaterialAsync(id));

    [HttpPost("variants")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> CreateVariant([FromBody] MaterialVariantRequest request) =>
        ToResult(await _service.CreateVariantAsync(request));

    [HttpGet("{materialId}/variants")]
    public async Task<IActionResult> GetVariants(int materialId) =>
        ToResult(await _service.GetVariantsByMaterialAsync(materialId));

    [HttpGet("variants/{variantId:int}")]
    public async Task<IActionResult> GetVariantById(int variantId) =>
        ToResult(await _service.GetVariantByIdAsync(variantId));

    [HttpPut("variants/{variantId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> UpdateVariant(int variantId, [FromBody] MaterialVariantRequest request) =>
        ToResult(await _service.UpdateVariantAsync(variantId, request));

    [HttpDelete("variants/{variantId}")]
    [Authorize(Roles = "ADMIN")]
    public async Task<IActionResult> DeleteVariant(int variantId) =>
        ToResult(await _service.DeleteVariantAsync(variantId));

    private ObjectResult ToResult(ApiResponse response) => StatusCode((int)response.StatusCode, response);
}
