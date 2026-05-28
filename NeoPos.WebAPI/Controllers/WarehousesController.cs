using BusinessLayer.DTOs.Warehouse;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WarehousesController : ControllerBase
{
    private readonly IWarehouseService _warehouseService;

    public WarehousesController(IWarehouseService warehouseService)
    {
        _warehouseService = warehouseService;
    }

    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetAll(Guid companyId)
    {
        var result = await _warehouseService.GetAllByCompanyIdAsync(companyId);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create(WarehousePostDto dto)
    {
        try
        {
            await _warehouseService.CreateAsync(dto);
            return Ok(new { message = "Anbar yaradıldı!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, WarehousePostDto dto)
    {
        try
        {
            await _warehouseService.UpdateAsync(id, dto);
            return Ok(new { message = "Məlumat yeniləndi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        try
        {
            await _warehouseService.DeleteAsync(id);
            return Ok(new { message = "Anbar silindi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{id}/set-default-sale")]
    public async Task<IActionResult> SetDefault(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _warehouseService.SetDefaultSaleWarehouseAsync(id, companyId);
            return Ok(new { message = "Satış anbarı təyin edildi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}