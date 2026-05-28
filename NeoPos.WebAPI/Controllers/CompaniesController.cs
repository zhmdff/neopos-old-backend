using BusinessLayer.DTOs.Company;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;
    private readonly IBossTelegramChatService _bossTelegramChatService;

    public CompaniesController(ICompanyService companyService, IBossTelegramChatService bossTelegramChatService)
    {
        _companyService = companyService;
        _bossTelegramChatService = bossTelegramChatService;
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        try
        {
            var result = await _companyService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update(
        [FromForm] CompanyPutDto dto,
        IFormFile? logoFile,
        IFormFile? posLockScreenFile = null,
        IFormFile? customerDisplayLockScreenFile = null)
    {
        try
        {
            var result = await _companyService.UpdateAsync(dto, logoFile, posLockScreenFile, customerDisplayLockScreenFile);
            if (result)
                return Ok(new { message = "Şirkət məlumatları uğurla yeniləndi!" });

            return BadRequest(new { message = "Yenilənmə zamanı xəta baş verdi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Terminal: avtomatik kassa növbəsi (Bakı vaxtı) — şirkət üzrə serverdə saxlanır.</summary>
    [HttpPut("{companyId:guid}/auto-cash-shift")]
    [Authorize]
    public async Task<IActionResult> UpdateAutoCashShift(Guid companyId, [FromBody] AutoCashShiftConfigPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var uid))
                return Unauthorized();

            var result = await _companyService.UpdateAutoCashShiftConfigAsync(companyId, uid, dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Boss: kassa/mətbəx printeri + çek dizayn ayarları (şirkət üzrə qalıcı).</summary>
    [HttpPut("{companyId:guid}/receipt-design")]
    [Authorize]
    public async Task<IActionResult> UpdateReceiptDesign(Guid companyId, [FromBody] CompanyReceiptDesignPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var uid))
                return Unauthorized();

            var result = await _companyService.UpdateReceiptDesignAsync(companyId, uid, dto);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Terminal: «mətbəxə göndərilmiş sətir silinəndə təsdiq» seçimi — şirkət üzrə serverdə (Boss menyu görünürlüyü üçün).
    /// </summary>
    [HttpPut("{companyId:guid}/terminal-line-delete-confirm")]
    [Authorize]
    public async Task<IActionResult> UpdateTerminalLineDeleteConfirm(
        Guid companyId,
        [FromBody] TerminalLineDeleteConfirmPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var result = await _companyService.UpdateTerminalLineDeleteConfirmEnabledAsync(companyId, dto.Enabled);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Terminal menyu: kateqoriya üstündə şöbə seçimi və məhsul süzülməsi (şirkət üzrə, default söndürülüb).
    /// </summary>
    [HttpPut("{companyId:guid}/menu-filter-by-workshop")]
    [Authorize]
    public async Task<IActionResult> UpdateMenuFilterByWorkshop(
        Guid companyId,
        [FromBody] MenuFilterByWorkshopPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var result = await _companyService.UpdateMenuFilterByWorkshopAsync(companyId, dto.Enabled);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Terminal (Electron) bildirişlərində bot token saxlananda — ofisiant silinmə təsdiqi üçün serverdə istifadə olunur.
    /// </summary>
    [HttpPut("{companyId:guid}/telegram-bot-token")]
    [Authorize]
    public async Task<IActionResult> UpdateTelegramBotToken(
        Guid companyId,
        [FromBody] CompanyTelegramBotTokenPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var result = await _companyService.UpdateTelegramBotTokenFromTerminalAsync(companyId, dto.Token);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Electron-da «Kod al» + botda /link ilə qoşulan Telegram chat id-lərini serverə yazır.
    /// Əl ilə chat id tapmadan ofisiant silinmə təsdiqi mesajı üçün kifayətdir (token da serverdə olsun).
    /// </summary>
    [HttpPost("{companyId:guid}/sync-telegram-chat-subscribers")]
    [Authorize]
    public async Task<IActionResult> SyncTelegramChatSubscribers(
        Guid companyId,
        [FromBody] CompanySyncTelegramChatSubscribersDto dto,
        CancellationToken ct)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var uid))
                return Unauthorized();

            var list = dto.ChatIds ?? new List<long>();
            await _bossTelegramChatService.SyncSubscriberChatIdsAsync(companyId, uid, list, ct);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Terminal «Bildiriş seçimləri» — oxuma (şirkət DB).</summary>
    [HttpGet("{companyId:guid}/telegram-notify-prefs")]
    [Authorize]
    public async Task<IActionResult> GetTelegramNotifyPrefs(Guid companyId)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            var prefs = await _companyService.GetTelegramNotifyPrefsAsync(companyId);
            return Ok(new { prefs = prefs ?? new Dictionary<string, bool>() });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Terminal «Bildiriş seçimləri» — server audit Telegram bildirişləri üçün.</summary>
    [HttpPut("{companyId:guid}/telegram-notify-prefs")]
    [Authorize]
    public async Task<IActionResult> UpdateTelegramNotifyPrefs(
        Guid companyId,
        [FromBody] CompanyTelegramNotifyPrefsPutDto dto)
    {
        try
        {
            var claimCo = User?.FindFirst("CompanyId")?.Value;
            if (!Guid.TryParse(claimCo, out var cidClaim) || cidClaim != companyId)
                return Forbid();

            await _companyService.UpdateTelegramNotifyPrefsFromTerminalAsync(companyId, dto.Prefs);
            return Ok(new { ok = true });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}