using BusinessLayer.DTOs.BossTelegram;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

/// <summary>
/// Ofisiant brauzerində silinmə təsdiqi üçün serverdən Telegram — alıcı chat ID-ləri burada saxlanılır.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class BossTelegramChatsController : ControllerBase
{
    private readonly IBossTelegramChatService _svc;

    public BossTelegramChatsController(IBossTelegramChatService svc)
    {
        _svc = svc;
    }

    [Authorize]
    [HttpGet]
    public async Task<ActionResult<List<BossTelegramChatRowDto>>> List(CancellationToken ct)
    {
        if (!CanManage()) return Forbid();
        var companyId = GetCompanyId();
        if (!companyId.HasValue) return Unauthorized(new { message = "Token məlumatı tapılmadı." });
        var list = await _svc.ListAsync(companyId.Value, ct);
        return Ok(list);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Link([FromBody] BossTelegramChatLinkDto body, CancellationToken ct)
    {
        if (!CanManage()) return Forbid();
        var userId = GetUserId();
        var companyId = GetCompanyId();
        if (!userId.HasValue || !companyId.HasValue)
            return Unauthorized(new { message = "Token məlumatı tapılmadı." });
        if (body.ChatId == 0)
            return BadRequest(new { message = "ChatId tələb olunur (məsələn qrup üçün mənfi ədəd)." });
        await _svc.LinkAsync(companyId.Value, userId.Value, body.ChatId, ct);
        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpDelete("{chatId:long}")]
    public async Task<IActionResult> Unlink(long chatId, CancellationToken ct)
    {
        if (!CanManage()) return Forbid();
        var companyId = GetCompanyId();
        if (!companyId.HasValue) return Unauthorized(new { message = "Token məlumatı tapılmadı." });
        await _svc.UnlinkAsync(companyId.Value, chatId, ct);
        return Ok(new { ok = true });
    }

    /// <summary>Boss JWT — ofisiant sessiyası ilə chat əlavə etmək olmaz.</summary>
    private bool CanManage()
    {
        if (string.Equals(User.FindFirst("IsWaiterSession")?.Value, "true", StringComparison.OrdinalIgnoreCase))
            return false;
        return GetUserId().HasValue && GetCompanyId().HasValue;
    }

    private Guid? GetUserId()
    {
        var id = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(id, out var g) ? g : null;
    }

    private Guid? GetCompanyId()
    {
        var id = User.FindFirst("CompanyId")?.Value;
        return Guid.TryParse(id, out var g) ? g : null;
    }
}
