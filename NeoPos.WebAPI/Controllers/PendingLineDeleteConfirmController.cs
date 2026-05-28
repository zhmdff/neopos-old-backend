using BusinessLayer.DTOs.PendingLineDelete;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class PendingLineDeleteConfirmController : ControllerBase
{
    private readonly IPendingLineDeleteConfirmService _svc;

    public PendingLineDeleteConfirmController(IPendingLineDeleteConfirmService svc)
    {
        _svc = svc;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromQuery] Guid companyId, [FromBody] PendingLineDeleteRegisterDto dto, CancellationToken ct)
    {
        if (!ValidateCompany(companyId))
            return Forbid();
        try
        {
            await _svc.RegisterAsync(companyId, dto, ct);
            return Ok(new { ok = true });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("active")]
    public async Task<IActionResult> Active([FromQuery] Guid companyId, CancellationToken ct)
    {
        if (!ValidateCompany(companyId))
            return Forbid();
        var list = await _svc.GetActiveAsync(companyId, ct);
        return Ok(list);
    }

    [HttpGet("{pendingId}/status")]
    public async Task<IActionResult> Status([FromQuery] Guid companyId, string pendingId, CancellationToken ct)
    {
        if (!ValidateCompany(companyId))
            return Forbid();
        var (status, accepted) = await _svc.GetStatusAsync(companyId, pendingId, ct);
        return Ok(new { status, accepted });
    }

    [HttpPost("{pendingId}/resolve")]
    public async Task<IActionResult> Resolve(
        [FromQuery] Guid companyId,
        string pendingId,
        [FromBody] PendingLineDeleteResolveDto dto,
        CancellationToken ct)
    {
        if (!ValidateCompany(companyId))
            return Forbid();

        var (status, accepted) = await _svc.TryResolveAsync(companyId, pendingId, dto.Accepted, ct);
        return Ok(new { status, accepted });
    }

    private bool ValidateCompany(Guid companyId)
    {
        var claim = User.FindFirst("CompanyId")?.Value;
        return Guid.TryParse(claim, out var g) && g == companyId;
    }
}
