using BusinessLayer.DTOs.CashShift;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class ShiftExpensesController : ControllerBase
{
    private readonly IShiftExpenseService _shiftExpenseService;

    public ShiftExpensesController(IShiftExpenseService shiftExpenseService)
    {
        _shiftExpenseService = shiftExpenseService;
    }

    private bool TryValidateCompany(Guid companyId, out IActionResult error)
    {
        error = null!;
        var claimCompany = User?.FindFirst("CompanyId")?.Value;
        if (!Guid.TryParse(claimCompany, out var jwtCompany) || jwtCompany != companyId)
        {
            error = BadRequest(new { message = "Şirkət uyğunsuzluğu." });
            return false;
        }

        return true;
    }

    private bool TryGetUserId(out Guid userId, out IActionResult error)
    {
        error = null!;
        userId = Guid.Empty;
        var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(idClaim) || idClaim.StartsWith("waiter:", StringComparison.OrdinalIgnoreCase))
        {
            error = Unauthorized();
            return false;
        }

        if (!Guid.TryParse(idClaim, out userId))
        {
            error = Unauthorized();
            return false;
        }

        return true;
    }

    /// <summary>Aktiv növbənin xərcləri (terminal).</summary>
    [HttpGet("active")]
    public async Task<IActionResult> ListActive([FromQuery] Guid companyId)
    {
        if (!TryValidateCompany(companyId, out var err))
            return err;
        var items = await _shiftExpenseService.ListActiveShiftAsync(companyId);
        return Ok(items);
    }

    /// <summary>Bütün tarixçə — Boss (səhifələmə + tarix filtri).</summary>
    [HttpGet("history")]
    public async Task<IActionResult> History(
        [FromQuery] Guid companyId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] Guid? cashShiftId = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        if (!TryValidateCompany(companyId, out var err))
            return err;

        var (items, total, totalAmount) = await _shiftExpenseService.ListHistoryAsync(companyId, from, to, page, pageSize, cashShiftId);
        return Ok(new { items, totalCount = total, totalAmount, page, pageSize });
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] ShiftExpensePostDto dto)
    {
        if (!TryValidateCompany(dto.CompanyId, out var err))
            return err;
        if (!TryGetUserId(out var userId, out var uerr))
            return uerr;

        var username = User?.FindFirst(ClaimTypes.Name)?.Value
                       ?? User?.Identity?.Name
                       ?? "terminal";

        try
        {
            var created = await _shiftExpenseService.AddAsync(dto, userId, username ?? "user");
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        if (!TryValidateCompany(companyId, out var err))
            return err;
        if (!TryGetUserId(out var userId, out var uerr))
            return uerr;

        var username = User?.FindFirst(ClaimTypes.Name)?.Value
                       ?? User?.Identity?.Name
                       ?? userId.ToString();

        try
        {
            await _shiftExpenseService.DeleteAsync(id, companyId, userId, username);
            return Ok(new { message = "Silindi" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
