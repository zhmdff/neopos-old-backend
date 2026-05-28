using BusinessLayer.DTOs.Role;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class RolesController : ControllerBase
{
    private readonly IRoleService _roleService;

    public RolesController(IRoleService roleService) => _roleService = roleService;

    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] Guid companyId)
    {
        try
        {
            var roles = await _roleService.GetAllAsync(companyId);
            return Ok(roles);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            var role = await _roleService.GetByIdAsync(id, companyId);
            return Ok(role);
        }
        catch (Exception ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RolePostDto dto)
    {
        try
        {
            await _roleService.CreateAsync(dto);
            return StatusCode(201, new { message = "Vəzifə yaradıldı." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] RolePutDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            // Əgər query-dən gəlirsə, onu əsas götürək (təhlükəsizlik üçün)
            if (companyId != Guid.Empty)
            {
                dto.CompanyId = companyId;
            }

            if (dto.Id == Guid.Empty || dto.CompanyId == Guid.Empty)
            {
                return BadRequest(new { message = "İd və ya Şirkət məlumatı çatışmır!" });
            }

            await _roleService.UpdateAsync(dto);
            return Ok(new { message = "Vəzifə yeniləndi." });
        }
        catch (Exception ex)
        {
            // Bura düşəndə artıq konkret səbəbi mesajda görəcəksən
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(Guid id, [FromQuery] Guid companyId)
    {
        try
        {
            await _roleService.DeleteAsync(id, companyId);
            return Ok(new { message = "Vəzifə silindi." });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}