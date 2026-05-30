using BusinessLayer.DTOs.Print;
using BusinessLayer.Printing;
using BusinessLayer.Services.Implementations;
using DAL.Server.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

/// <summary>Staff çap: Boss-da saxlanılan receiptDesignSettingsJson ilə ESC/POS baytları.</summary>
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class PrintController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly ITcpPrinterService _tcpPrinterService;

    public PrintController(AppDbContext db, ITcpPrinterService tcpPrinterService)
    {
        _db = db;
        _tcpPrinterService = tcpPrinterService;
    }

    [HttpPost("kassa-escpos")]
    public async Task<IActionResult> RenderKassaEscPos([FromBody] RenderKassaEscPosDto dto)
    {
        if (!TryResolveCompanyId(dto.CompanyId, out var companyId))
            return Forbid();

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);
        if (company == null)
            return NotFound(new { message = "Şirkət tapılmadı." });

        var ctx = KassaReceiptContextMapper.FromDto(dto, company);
        var bytes = KassaEscPosRenderer.Render(company.ReceiptDesignSettingsJson, ctx);
        return Ok(new { dataBase64 = Convert.ToBase64String(bytes) });
    }

    [HttpPost("kitchen-escpos")]
    public async Task<IActionResult> RenderKitchenEscPos([FromBody] RenderKitchenEscPosDto dto)
    {
        if (!TryResolveCompanyId(dto.CompanyId, out var companyId))
            return Forbid();

        var company = await _db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId);
        if (company == null)
            return NotFound(new { message = "Şirkət tapılmadı." });

        var openTime = ParseDateTime(dto.OpenTime);
        var beep = string.IsNullOrWhiteSpace(dto.BeepMode) ? "default" : dto.BeepMode.Trim();
        var bytes = _tcpPrinterService.GenerateKitchenEscPos(
            company.ReceiptDesignSettingsJson,
            dto.WorkshopName ?? "",
            dto.HallName ?? "",
            dto.TableName ?? "",
            dto.WaiterName ?? "",
            openTime,
            dto.Items ?? [],
            beep);

        return Ok(new { dataBase64 = Convert.ToBase64String(bytes) });
    }

    private bool TryResolveCompanyId(Guid requestedCompanyId, out Guid companyId)
    {
        companyId = Guid.Empty;
        var claimCo = User?.FindFirst("CompanyId")?.Value;
        if (!Guid.TryParse(claimCo, out var cidClaim))
            return false;

        if (requestedCompanyId != Guid.Empty && requestedCompanyId != cidClaim)
            return false;

        companyId = cidClaim;
        return true;
    }

    private static DateTime? ParseDateTime(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        return DateTime.TryParse(raw, out var dt) ? dt : null;
    }
}
