using BusinessLayer.DTOs.Supplier;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    [HttpGet("company/{companyId}")]
    public async Task<IActionResult> GetAll(Guid companyId)
    {
        var result = await _supplierService.GetAllByCompanyIdAsync(companyId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Get(Guid id)
    {
        try
        {
            var result = await _supplierService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(SupplierPostDto dto)
    {
        try
        {
            await _supplierService.CreateAsync(dto);
            return Ok(new { message = "Tədarükçü uğurla əlavə edildi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Update(Guid id, SupplierPostDto dto)
    {
        try
        {
            await _supplierService.UpdateAsync(id, dto);
            return Ok(new { message = "Məlumatlar yeniləndi." });
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
            await _supplierService.DeleteAsync(id);
            return Ok(new { message = "Tədarükçü silindi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}