using BusinessLayer.DTOs.Category;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;

    public CategoriesController(ICategoryService categoryService)
    {
        _categoryService = categoryService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid companyId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = 100, // Take-i 100 etdim ki, POS-da hamısı görünsün
        [FromQuery] string? search = null,
        [FromQuery] Guid? parentId = null) // 🔥 Bura mütləq əlavə olunmalıdır
    {
        // Service-ə beşinci parametr olaraq parentId-ni ötürürük
        var result = await _categoryService.GetAllAsync(companyId, skip, take, search, parentId);
        return Ok(result);
    }

    [HttpPost("update-orders")]
    public async Task<IActionResult> UpdateOrders([FromQuery] Guid companyId, [FromBody] List<CategoryOrderUpdateDto> dtos)
    {
        try
        {
            await _categoryService.UpdateOrdersAsync(companyId, dtos);
            return Ok(new { message = "Kateqoriya sıralaması uğurla yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromForm] CategoryPostDto dto)
    {
        try
        {
            // Frontend DTO daxilində CompanyId göndərməlidir
            var id = await _categoryService.CreateAsync(dto);
            return Ok(new { id, message = "Kateqoriya uğurla əlavə edildi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromForm] CategoryPutDto dto)
    {
        try
        {
            await _categoryService.UpdateAsync(dto);
            return Ok(new { message = "Kateqoriya məlumatları yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _categoryService.DeleteAsync(id, companyId);
            return Ok(new { message = "Kateqoriya silindi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}