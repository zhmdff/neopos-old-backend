using BusinessLayer.DTOs.User;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService) => _userService = userService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId)
    {
        var users = await _userService.GetAllAsync(companyId);
        return Ok(users);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            Guid? viewerId = null;
            var idClaim = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (Guid.TryParse(idClaim, out var parsed))
                viewerId = parsed;

            var user = await _userService.GetByIdAsync(id, companyId, viewerId);
            return Ok(user);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] UserPostDto dto)
    {
        try
        {
            await _userService.CreateAsync(dto);
            return StatusCode(201, new { message = "İstifadəçi uğurla yaradıldı." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] UserPutDto dto)
    {
        try
        {
            await _userService.UpdateAsync(dto);
            return Ok(new { message = "Məlumatlar yeniləndi." });
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
            await _userService.DeleteAsync(id, companyId);
            return Ok(new { message = "İstifadəçi silindi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}