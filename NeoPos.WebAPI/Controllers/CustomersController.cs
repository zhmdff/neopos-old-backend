using BusinessLayer.DTOs.Customer;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace NeoPos.WebAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search(
        [FromQuery] Guid companyId,
        [FromQuery] string? q = null,
        [FromQuery] int take = 40,
        [FromQuery] int skip = 0)
    {
        try
        {
            var list = await _customerService.SearchAsync(companyId, q, take, skip);
            return Ok(list);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CustomerPostDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var created = await _customerService.CreateAsync(dto, companyId);
            return Ok(created);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] CustomerPostDto dto, [FromQuery] Guid companyId)
    {
        try
        {
            var updated = await _customerService.UpdateAsync(id, dto, companyId);
            return Ok(updated);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
