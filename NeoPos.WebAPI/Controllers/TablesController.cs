using BusinessLayer.DTOs.Table;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TablesController : ControllerBase
{
    private readonly ITableService _tableService;

    public TablesController(ITableService tableService) => _tableService = tableService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId)
    {
        return Ok(await _tableService.GetAllAsync(companyId));
    }

    [HttpPut("update-orders")]
    public async Task<IActionResult> UpdateOrders([FromQuery] Guid companyId, [FromBody] List<TableOrderUpdateDto> dtos)
    {
        try
        {
            await _tableService.UpdateOrdersAsync(companyId, dtos);
            return Ok(new { message = "Masaların sıralaması yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(TablePostDto dto)
    {
        try
        {
            await _tableService.CreateAsync(dto);
            return Ok(new { message = "Masa uğurla əlavə edildi!" });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPut]
    public async Task<IActionResult> Update(TablePutDto dto)
    {
        try
        {
            await _tableService.UpdateAsync(dto);
            return Ok(new { message = "Masa məlumatları yeniləndi!" });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _tableService.DeleteAsync(id, companyId);
            return Ok(new { message = "Masa silindi!" });
        }
        catch (Exception ex) { return BadRequest(new { message = ex.Message }); }
    }

    [HttpGet("ByHall/{hallId}")]
    public async Task<IActionResult> GetByHall(Guid hallId, [FromQuery] Guid companyId)
    {
        var result = await _tableService.GetByHallIdAsync(hallId, companyId);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid companyId)
    {
        var result = await _tableService.GetByIdAsync(id, companyId);
        if (result == null) return NotFound(new { message = "Masa tapılmadı!" });
        return Ok(result);
    }
}