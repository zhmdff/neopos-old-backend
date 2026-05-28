using BusinessLayer.DTOs.ProductVariant;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProductVariantsController : ControllerBase
{
    private readonly IProductVariantService _service;

    public ProductVariantsController(IProductVariantService service)
    {
        _service = service;
    }

    [HttpGet("by-product/{productId}")]
    public async Task<IActionResult> GetByProduct(Guid productId, [FromQuery] Guid companyId)
    {
        var res = await _service.GetByProductAsync(productId, companyId);
        return Ok(res);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ProductVariantPostDto dto, [FromQuery] Guid companyId)
    {
        dto.CompanyId = companyId;
        try
        {
            var created = await _service.CreateAsync(dto);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ProductVariantPutDto dto, [FromQuery] Guid companyId)
    {
        dto.CompanyId = companyId;
        try
        {
            var updated = await _service.UpdateAsync(dto);
            return Ok(updated);
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
            await _service.DeleteAsync(id, companyId);
            return Ok(new { message = "Variant silindi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

