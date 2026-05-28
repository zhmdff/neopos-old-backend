using BusinessLayer.DTOs.Workshop;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WorkshopsController : ControllerBase
{
    private readonly IWorkshopService _workshopService;

    public WorkshopsController(IWorkshopService workshopService)
    {
        _workshopService = workshopService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId)
    {
        return Ok(await _workshopService.GetAllAsync(companyId));
    }

    [HttpPost]
    public async Task<IActionResult> Create(WorkshopPostDto dto)
    {
        try
        {
            await _workshopService.CreateAsync(dto);
            return Ok(new { message = "Emalatxana uğurla yaradıldı!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(WorkshopPutDto dto)
    {
        try
        {
            await _workshopService.UpdateAsync(dto);
            return Ok(new { message = "Emalatxana məlumatları yeniləndi!" });
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
            await _workshopService.DeleteAsync(id, companyId);
            return Ok(new { message = "Emalatxana uğurla silindi!" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}