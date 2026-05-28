using BusinessLayer.DTOs.Hall;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

[Route("api/[controller]")]
[ApiController]
public class HallsController : ControllerBase
{
    private readonly IHallService _hallService;

    public HallsController(IHallService hallService)
    {
        _hallService = hallService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId)
    {
        return Ok(await _hallService.GetAllAsync(companyId));
    }

    // 405 xətasını aradan qaldırmaq üçün HttpPut edildi
    [HttpPut("update-orders")]
    public async Task<IActionResult> UpdateOrders([FromQuery] Guid companyId, [FromBody] List<HallOrderUpdateDto> dtos)
    {
        try
        {
            await _hallService.UpdateOrdersAsync(companyId, dtos);
            return Ok(new { message = "Zalların sıralaması yeniləndi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create(HallPostDto dto)
    {
        try
        {
            await _hallService.CreateAsync(dto);
            return Ok(new { message = "Zal uğurla yaradıldı!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(HallPutDto dto)
    {
        try
        {
            await _hallService.UpdateAsync(dto);
            return Ok(new { message = "Zal uğurla yeniləndi!" });
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
            await _hallService.DeleteAsync(id, companyId);
            return Ok(new { message = "Zal silindi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}