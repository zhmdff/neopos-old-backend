using BusinessLayer.DTOs.Auth;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITenantBootstrapService _tenantBootstrapService;

    public AuthController(IAuthService authService, ITenantBootstrapService tenantBootstrapService)
    {
        _authService = authService;
        _tenantBootstrapService = tenantBootstrapService;
    }

    /// <summary>
    /// Gizli yol: şirkət + admin rolu + ilk user. NeoPos:TenantBootstrapSecret boşdursa işləmir.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("tenant-bootstrap")]
    public async Task<IActionResult> TenantBootstrap([FromBody] TenantBootstrapRequestDto request)
    {
        try
        {
            var result = await _tenantBootstrapService.BootstrapAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequestDTO request)
    {
        try
        {
            var result = await _authService.LoginAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("pin-login")]
    public async Task<IActionResult> PinLogin([FromBody] PinLoginRequestDTO request)
    {
        try
        {
            var result = await _authService.PinLoginAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [AllowAnonymous]
    [HttpPost("waiter-shift-login")]
    public async Task<IActionResult> WaiterShiftLogin([FromBody] WaiterShiftLoginRequestDTO request)
    {
        try
        {
            var result = await _authService.WaiterShiftLoginAsync(request);
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Biznes qaydası xətasıdır (JWT yox); 401 brauzerdə "Unauthorized" kimi çaşdırır, ofisiant axios 401-də token silir.
            return BadRequest(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("switch-company")]
    public async Task<IActionResult> SwitchCompany([FromBody] SwitchCompanyRequestDTO request)
    {
        try
        {
            var idClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) throw new Exception("İstifadəçi tapılmadı.");
            var result = await _authService.SwitchCompanyAsync(userId, request.CompanyId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    [Authorize]
    [HttpPost("link-accounts")]
    public async Task<IActionResult> LinkAccounts([FromBody] LinkAccountsRequestDTO request)
    {
        try
        {
            var idClaim = User?.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(idClaim, out var userId)) throw new Exception("İstifadəçi tapılmadı.");
            var result = await _authService.LinkAccountsAsync(userId, request.OtherUserId);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }
}