using BusinessLayer.DTOs.CashShift;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CashShiftsController : ControllerBase
{
    private readonly ICashShiftService _cashShiftService;

    public CashShiftsController(ICashShiftService cashShiftService)
    {
        _cashShiftService = cashShiftService;
    }

    [HttpGet("active/{companyId}")]
    public async Task<IActionResult> GetActive(Guid companyId)
    {
        var result = await _cashShiftService.GetActiveShiftAsync(companyId);
        return Ok(result);
    }

    [HttpPost("regenerate-waiter-code/{companyId}")]
    public async Task<IActionResult> RegenerateWaiterCode(Guid companyId)
    {
        var result = await _cashShiftService.RegenerateWaiterCodeAsync(companyId);
        if (result == null) return BadRequest(new { message = "Açıq növbə yoxdur." });
        return Ok(result);
    }

    [HttpPost("open")]
    public async Task<IActionResult> Open(CashShiftOpenDto dto)
    {
        try
        {
            await _cashShiftService.OpenShiftAsync(dto);
            return Ok(new { message = "Növbə uğurla açıldı" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("close")]
    public async Task<IActionResult> Close(CashShiftCloseDto dto)
    {
        try
        {
            await _cashShiftService.CloseShiftAsync(dto);
            return Ok(new { message = "Növbə uğurla bağlandı" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("all-shifts")]
    public async Task<IActionResult> GetHistory(
    [FromQuery] Guid companyId,
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 10)
    {
        var result = await _cashShiftService.GetShiftHistoryAsync(companyId, page, pageSize);

        return Ok(result);
    }

    [HttpGet("active-shift-orders/{companyId}")]
    public async Task<IActionResult> GetActiveShiftOrders(Guid companyId, [FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        var result = await _cashShiftService.GetActiveShiftOrdersAsync(companyId, page, pageSize);
        return Ok(result);
    }
}