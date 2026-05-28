using BusinessLayer.DTOs.ProductSet;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductSetsController : ControllerBase
{
    private readonly IProductSetService _setService;

    public ProductSetsController(IProductSetService setService)
    {
        _setService = setService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductSetPostDto dto)
    {
        // DTO daxilində CompanyId gəlməlidir
        var result = await _setService.CreateSetAsync(dto);
        return Ok(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] Guid companyId, // Şirkət ID-si sorğulanır
        int skip = 0,
        int take = 10,
        string? search = null,
        Guid? categoryId = null,
        Guid? workshopId = null)
    {
        var result = await _setService.GetAllSetsAsync(companyId, skip, take, search, categoryId, workshopId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid companyId)
    {
        var result = await _setService.GetSetByIdAsync(id, companyId);
        return Ok(result);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        await _setService.DeleteSetAsync(id, companyId);
        return Ok(new { message = "Set uğurla silindi." });
    }
}