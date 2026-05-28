using BusinessLayer.DTOs.Company;
using BusinessLayer.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace NeoPos.WebAPI.Controllers;

[Route("api/companies/{companyId:guid}/payment-methods")]
[ApiController]
public class CompanyPaymentMethodsController : ControllerBase
{
    private readonly ICompanyPaymentMethodService _service;

    public CompanyPaymentMethodsController(ICompanyPaymentMethodService service) => _service = service;

    /// <summary>Terminal + Boss: şirkətin əlavə ödəniş üsulları siyahısı.</summary>
    [HttpGet]
    public async Task<IActionResult> List(Guid companyId)
    {
        if (companyId == Guid.Empty) return BadRequest("companyId");
        var list = await _service.ListAsync(companyId);
        return Ok(list);
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Add(Guid companyId, [FromBody] CompanyPaymentMethodPostDto dto)
    {
        if (companyId == Guid.Empty) return BadRequest("companyId");
        if (!TryGetAuthorizedCompanyId(out var claimCo) || claimCo != companyId)
            return Forbid();

        var uid = User?.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "boss";
        try
        {
            var row = await _service.AddAsync(companyId, dto, uid);
            return Ok(row);
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid companyId, Guid id, [FromBody] CompanyPaymentMethodPutDto dto)
    {
        if (companyId == Guid.Empty) return BadRequest("companyId");
        if (!TryGetAuthorizedCompanyId(out var claimCo) || claimCo != companyId)
            return Forbid();

        try
        {
            var ok = await _service.UpdateAsync(companyId, id, dto);
            if (!ok) return NotFound();
            return Ok(new { message = "OK" });
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid companyId, Guid id)
    {
        if (companyId == Guid.Empty) return BadRequest("companyId");
        if (!TryGetAuthorizedCompanyId(out var claimCo) || claimCo != companyId)
            return Forbid();

        var ok = await _service.DeleteAsync(companyId, id);
        if (!ok) return NotFound();
        return Ok(new { message = "OK" });
    }

    private bool TryGetAuthorizedCompanyId(out Guid companyId)
    {
        companyId = Guid.Empty;
        var raw = User?.FindFirst("CompanyId")?.Value;
        return Guid.TryParse(raw, out companyId);
    }
}
