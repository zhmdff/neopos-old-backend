using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PushSubscriptionsController : ControllerBase
{
    private readonly IBossWebPushService _webPush;

    public PushSubscriptionsController(IBossWebPushService webPush)
    {
        _webPush = webPush;
    }

    [AllowAnonymous]
    [HttpGet("vapid-public-key")]
    public ActionResult<object> GetVapidPublicKey()
    {
        var k = _webPush.GetVapidPublicKey();
        return Ok(new { publicKey = k });
    }

    public class PushSubscriptionBodyDto
    {
        public string? Endpoint { get; set; }
        public PushKeysDto? Keys { get; set; }
    }

    public class PushKeysDto
    {
        public string? P256dh { get; set; }
        public string? Auth { get; set; }
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Register([FromBody] PushSubscriptionBodyDto body, CancellationToken ct)
    {
        if (!CanRegisterBossWebPush()) return Forbid();

        var userId = GetUserId();
        var companyId = GetCompanyId();
        if (!userId.HasValue || !companyId.HasValue)
            return Unauthorized(new { message = "Token məlumatı tapılmadı." });

        var endpoint = body.Endpoint?.Trim() ?? string.Empty;
        var p256dh = body.Keys?.P256dh?.Trim() ?? string.Empty;
        var auth = body.Keys?.Auth?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(p256dh) || string.IsNullOrEmpty(auth))
            return BadRequest(new { message = "Endpoint və keys tələb olunur." });

        await _webPush.UpsertSubscriptionAsync(userId.Value, companyId.Value, endpoint, p256dh, auth, ct);
        return Ok(new { ok = true });
    }

    [Authorize]
    [HttpDelete]
    public async Task<IActionResult> Unregister([FromBody] PushSubscriptionBodyDto body, CancellationToken ct)
    {
        if (!CanRegisterBossWebPush()) return Forbid();

        var userId = GetUserId();
        var companyId = GetCompanyId();
        if (!userId.HasValue || !companyId.HasValue)
            return Unauthorized(new { message = "Token məlumatı tapılmadı." });

        var endpoint = body.Endpoint?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(endpoint))
            return BadRequest(new { message = "Endpoint tələb olunur." });

        await _webPush.RemoveByEndpointAsync(userId.Value, companyId.Value, endpoint, ct);
        return Ok(new { ok = true });
    }

    /// <summary>Boss JWT (ofisiant sessiyası deyil) — istənilən şirkət istifadəçisi abunə ola bilər.</summary>
    private bool CanRegisterBossWebPush()
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
