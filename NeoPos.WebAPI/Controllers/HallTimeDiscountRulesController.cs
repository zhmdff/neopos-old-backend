using BusinessLayer.DTOs.HallTimeDiscount;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HallTimeDiscountRulesController : ControllerBase
{
    private readonly IHallTimeDiscountRuleService _service;

    public HallTimeDiscountRulesController(IHallTimeDiscountRuleService service)
    {
        _service = service;
    }

    [HttpGet("by-hall/{hallId:guid}")]
    public async Task<IActionResult> GetByHall(Guid hallId, [FromQuery] Guid companyId)
    {
        return Ok(await _service.GetByHallAsync(hallId, companyId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(HallTimeDiscountRulePostDto dto)
    {
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
    public async Task<IActionResult> Update(HallTimeDiscountRulePutDto dto)
    {
        try
        {
            await _service.UpdateAsync(dto);
            return Ok(new { message = "Yeniləndi" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _service.DeleteAsync(id, companyId);
            return Ok(new { message = "Silindi" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
