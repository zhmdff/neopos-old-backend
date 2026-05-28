using BusinessLayer.DTOs.Audit;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class AuditLogsController : ControllerBase
{
    private readonly IAuditLogService _auditLogService;

    public AuditLogsController(IAuditLogService auditLogService)
    {
        _auditLogService = auditLogService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLogs(
        [FromQuery] Guid companyId,
        [FromQuery] int take = 50,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        if (companyId == Guid.Empty)
            return BadRequest("ŞİRKƏT İD (CompanyId) MÜTLƏQDİR!");

        if (from.HasValue && to.HasValue && from.Value > to.Value)
            return BadRequest("«from» «to»-dan böyük ola bilməz.");

        var logs = await _auditLogService.GetCompanyLogsAsync(companyId, take, from, to);
        return Ok(logs);
    }

    [HttpGet("shift/{shiftId}")]
    public async Task<IActionResult> GetShiftLogs(Guid shiftId, [FromQuery] Guid companyId, [FromQuery] int take = 50)
    {
        if (companyId == Guid.Empty)
            return BadRequest("ŞİRKƏT İD (CompanyId) MÜTLƏQDİR!");

        try
        {
            var logs = await _auditLogService.GetShiftLogsAsync(shiftId, companyId, take);
            return Ok(logs);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> CreateLog([FromBody] AuditLogPostDto dto)
    {
        if (dto == null) return BadRequest();

        await _auditLogService.LogActionAsync(dto);
        return Ok(new { message = "HƏRƏKƏT QEYD EDİLDİ VƏ BİLDİRİŞ GÖNDƏRİLDİ." });
    }
}